using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BaGet.Core;

/// <remarks>
/// Identity's tables live in the same database as the packages, so a deployment keeps
/// one database. They are created by a migration whether or not accounts are enabled.
/// </remarks>
public abstract class AbstractContext<TContext> : IdentityDbContext<BaGetUser>, IContext where TContext : DbContext
{
    public const int DefaultMaxStringLength = 4000;

    public const int MaxPackageIdLength = 128;
    public const int MaxPackageVersionLength = 64;
    public const int MaxPackageMinClientVersionLength = 44;
    public const int MaxPackageLanguageLength = 20;
    public const int MaxPackageTitleLength = 256;
    public const int MaxPackageTypeNameLength = 512;
    public const int MaxPackageTypeVersionLength = 64;
    public const int MaxRepositoryTypeLength = 100;
    public const int MaxTargetFrameworkLength = 256;

    public const int MaxPackageDependencyVersionRangeLength = 256;

    public const int MaxCatalogCommitIdLength = 36;
    public const int MaxCatalogTypeLength = 32;

    public const int MaxAccountNameLength = 256;
    public const int MaxUserIdLength = 450;
    public const int MaxApiKeyDescriptionLength = 256;
    public const int MaxApiKeyHashLength = 64;

    public const int MaxRepositoryNameLength = RepositoryNames.MaxLength;
    public const int MaxRepositoryFormatLength = 32;
    public const int MaxProjectNameLength = 64;
    public const int MaxProjectDisplayNameLength = 256;

    public const int MaxValidationTypeLength = 64;

    public const int MaxAbuseReasonLength = 128;
    public const int MaxEmailLength = 256;

    private IRepositoryContext? _repository;

    public AbstractContext(DbContextOptions<TContext> options)
        : base(options)
    { }

    /// <summary>
    /// The repository this context reads and writes: every <see cref="IRepositoryScoped"/> query is filtered on it
    /// and new rows are stamped with it. Resolved from the application's DI scope on first use when the context
    /// was created by DI; a context built by hand (a database copy's target, design time) works in the default
    /// repository unless this is set. Not a security boundary on its own: <c>IgnoreQueryFilters()</c> sees all.
    /// </summary>
    public IRepositoryContext RepositoryScope
    {
        get => _repository ??= ResolveRepositoryScope() ?? DefaultRepositoryContext.Instance;
        set => _repository = value ?? throw new ArgumentNullException(nameof(value));
    }

    // AddDbContext hands the context the application's provider (the request scope); a hand-built context has none.
    private IRepositoryContext? ResolveRepositoryScope()
        => this.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>()
            ?.ApplicationServiceProvider?.GetService(typeof(IRepositoryContext)) as IRepositoryContext;

    /// <summary>
    /// The key the query filters compare against. Evaluated per query, so it follows
    /// <see cref="IRepositoryContext.Use"/> called after the context was created.
    /// </summary>
    protected int CurrentRepositoryKey => RepositoryScope.Current.Key;

    public DbSet<Package>? Packages { get; set; }
    public DbSet<PackageDependency>? PackageDependencies { get; set; }
    public DbSet<PackageType>? PackageTypes { get; set; }
    public DbSet<TargetFramework>? TargetFrameworks { get; set; }
    public DbSet<PackageDeprecation>? PackageDeprecations { get; set; }
    public DbSet<PackageVulnerability>? PackageVulnerabilities { get; set; }
    public DbSet<CatalogEntry>? CatalogLeafs { get; set; }
    public DbSet<Organization>? Organizations { get; set; }
    public DbSet<OrganizationMember>? OrganizationMembers { get; set; }
    public DbSet<PackageOwner>? PackageOwners { get; set; }
    public DbSet<ApiKey>? ApiKeys { get; set; }
    public DbSet<PackageValidation>? PackageValidations { get; set; }
    public DbSet<AbuseReport>? AbuseReports { get; set; }
    public DbSet<Repository>? Repositories { get; set; }
    public DbSet<Project>? Projects { get; set; }

    public Task<int> SaveChangesAsync() => SaveChangesAsync(default);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampRepository();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampRepository();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // A new row without a repository belongs to the current one. A row that already names one (a database copy,
    // an explicit move) is left alone.
    private void StampRepository()
    {
        foreach (var entry in ChangeTracker.Entries<IRepositoryScoped>().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity.RepositoryKey == 0) entry.Entity.RepositoryKey = CurrentRepositoryKey;
        }
    }

    public virtual async Task RunMigrationsAsync(CancellationToken cancellationToken)
        => await Database.MigrateAsync(cancellationToken);

    /// <inheritdoc/>
    /// <remarks>Does nothing by default: SQLite and MySQL advance their auto-increment counter from explicit keys.</remarks>
    public virtual Task BeginKeyedInsertAsync(IEntityType table, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc/>
    /// <remarks>Does nothing by default: SQLite and MySQL advance their auto-increment counter from explicit keys.</remarks>
    public virtual Task EndKeyedInsertAsync(IEntityType table, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public abstract bool IsUniqueConstraintViolationException(DbUpdateException exception);

    public virtual bool SupportsLimitInSubqueries => true;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Package>(BuildPackageEntity);
        builder.Entity<PackageDependency>(BuildPackageDependencyEntity);
        builder.Entity<PackageType>(BuildPackageTypeEntity);
        builder.Entity<TargetFramework>(BuildTargetFrameworkEntity);
        builder.Entity<PackageDeprecation>(BuildPackageDeprecationEntity);
        builder.Entity<PackageVulnerability>(BuildPackageVulnerabilityEntity);
        builder.Entity<CatalogEntry>(BuildCatalogEntryEntity);
        builder.Entity<Organization>(BuildOrganizationEntity);
        builder.Entity<OrganizationMember>(BuildOrganizationMemberEntity);
        builder.Entity<PackageOwner>(BuildPackageOwnerEntity);
        builder.Entity<ApiKey>(BuildApiKeyEntity);
        builder.Entity<PackageValidation>(BuildPackageValidationEntity);
        builder.Entity<AbuseReport>(BuildAbuseReportEntity);
        builder.Entity<Repository>(BuildRepositoryEntity);
        builder.Entity<Project>(BuildProjectEntity);

        // Repository isolation: a query in one repository never sees another's rows. The filter reads the
        // context's current repository per query.
        builder.Entity<Package>().HasQueryFilter(e => e.RepositoryKey == CurrentRepositoryKey);
        builder.Entity<PackageOwner>().HasQueryFilter(e => e.RepositoryKey == CurrentRepositoryKey);
        builder.Entity<CatalogEntry>().HasQueryFilter(e => e.RepositoryKey == CurrentRepositoryKey);
        builder.Entity<PackageValidation>().HasQueryFilter(e => e.RepositoryKey == CurrentRepositoryKey);
        builder.Entity<AbuseReport>().HasQueryFilter(e => e.RepositoryKey == CurrentRepositoryKey);

        // The column default is what the upgrade migration gives every row that predates repositories: they
        // belong to the default repository.
        builder.Entity<Package>().Property(e => e.RepositoryKey).HasDefaultValue(Repository.DefaultKey);
        builder.Entity<PackageOwner>().Property(e => e.RepositoryKey).HasDefaultValue(Repository.DefaultKey);
        builder.Entity<CatalogEntry>().Property(e => e.RepositoryKey).HasDefaultValue(Repository.DefaultKey);
        builder.Entity<PackageValidation>().Property(e => e.RepositoryKey).HasDefaultValue(Repository.DefaultKey);
        builder.Entity<AbuseReport>().Property(e => e.RepositoryKey).HasDefaultValue(Repository.DefaultKey);
    }

    private void BuildRepositoryEntity(EntityTypeBuilder<Repository> repository)
    {
        repository.HasKey(r => r.Key);
        repository.HasIndex(r => r.Name).IsUnique();

        repository.Property(r => r.Name).HasMaxLength(MaxRepositoryNameLength).IsRequired();
        repository.Property(r => r.Format).HasConversion<string>().HasMaxLength(MaxRepositoryFormatLength);
        repository.Property(r => r.Kind).HasConversion<string>().HasMaxLength(MaxRepositoryFormatLength);
    }

    private void BuildProjectEntity(EntityTypeBuilder<Project> project)
    {
        project.HasKey(p => p.Key);
        project.HasIndex(p => p.Name).IsUnique();

        project.Property(p => p.Name).HasMaxLength(MaxProjectNameLength).IsRequired();
        project.Property(p => p.DisplayName).HasMaxLength(MaxProjectDisplayNameLength);
    }

    private void BuildPackageEntity(EntityTypeBuilder<Package> package)
    {
        package.HasKey(p => p.Key);
        package.HasIndex(p => p.Id);
        package.HasIndex(p => new { p.RepositoryKey, p.Id, p.NormalizedVersionString })
            .IsUnique();

        package.Property(p => p.Id)
            .HasMaxLength(MaxPackageIdLength)
            .IsRequired();

        package.Property(p => p.NormalizedVersionString)
            .HasColumnName("Version")
            .HasMaxLength(MaxPackageVersionLength)
            .IsRequired();

        package.Property(p => p.OriginalVersionString)
            .HasColumnName("OriginalVersion")
            .HasMaxLength(MaxPackageVersionLength);

        package.Property(p => p.ReleaseNotes)
            .HasColumnName("ReleaseNotes");

        #pragma warning disable CS8620
        package.Property(p => p.Authors)
            .HasMaxLength(DefaultMaxStringLength)
            .HasConversion(StringArrayToJsonConverter.Instance)
            .Metadata.SetValueComparer(StringArrayComparer.Instance);
        #pragma warning restore CS8620

        #pragma warning disable CS8620
        package.Property(p => p.IconUrl)
            .HasConversion(UriToStringConverter.Instance)
            .HasMaxLength(DefaultMaxStringLength);
        #pragma warning restore CS8620

        #pragma warning disable CS8620
        package.Property(p => p.LicenseUrl)
            .HasConversion(UriToStringConverter.Instance)
            .HasMaxLength(DefaultMaxStringLength);
        #pragma warning restore CS8620

        #pragma warning disable CS8620
        package.Property(p => p.ProjectUrl)
            .HasConversion(UriToStringConverter.Instance)
            .HasMaxLength(DefaultMaxStringLength);
        #pragma warning restore CS8620

        #pragma warning disable CS8620
        package.Property(p => p.RepositoryUrl)
            .HasConversion(UriToStringConverter.Instance)
            .HasMaxLength(DefaultMaxStringLength);
        #pragma warning restore CS8620

        #pragma warning disable CS8620
        package.Property(p => p.Tags)
            .HasMaxLength(DefaultMaxStringLength)
            .HasConversion(StringArrayToJsonConverter.Instance)
            .Metadata.SetValueComparer(StringArrayComparer.Instance);
        #pragma warning restore CS8620

        package.Property(p => p.Description).HasMaxLength(DefaultMaxStringLength);
        package.Property(p => p.Language).HasMaxLength(MaxPackageLanguageLength);
        package.Property(p => p.MinClientVersion).HasMaxLength(MaxPackageMinClientVersionLength);
        package.Property(p => p.Summary).HasMaxLength(DefaultMaxStringLength);
        package.Property(p => p.Title).HasMaxLength(MaxPackageTitleLength);
        package.Property(p => p.RepositoryType).HasMaxLength(MaxRepositoryTypeLength);

        package.Ignore(p => p.Version);
        package.Ignore(p => p.IconUrlString);
        package.Ignore(p => p.LicenseUrlString);
        package.Ignore(p => p.ProjectUrlString);
        package.Ignore(p => p.RepositoryUrlString);

        // The dependency to package relationship is deliberately left optional. Making it
        // required, or giving it a cascade delete, changes the foreign key and so needs a
        // migration; SQLite cannot alter a foreign key, so consumers would have to
        // recreate their database and reindex every package. PackageDatabase therefore
        // deletes a package's dependency rows itself, so that none are left orphaned.
        // See: https://github.com/loic-sharma/BaGet/pull/174

        package.HasMany(p => p.PackageTypes)
            .WithOne(d => d.Package)
            .IsRequired();

        package.HasMany(p => p.TargetFrameworks)
            .WithOne(d => d.Package)
            .IsRequired();

        package.Property(p => p.RowVersion).IsRowVersion();
    }

    private void BuildPackageDeprecationEntity(EntityTypeBuilder<PackageDeprecation> deprecation)
    {
        deprecation.HasKey(d => d.Key);

        #pragma warning disable CS8620
        deprecation
            .Property(d => d.Reasons)
            .HasMaxLength(DefaultMaxStringLength)
            .HasConversion(StringArrayToJsonConverter.Instance)
            .Metadata.SetValueComparer(StringArrayComparer.Instance);
        #pragma warning restore CS8620

        deprecation.Property(d => d.Message).HasMaxLength(DefaultMaxStringLength);
        deprecation.Property(d => d.AlternatePackageId).HasMaxLength(MaxPackageIdLength);
        deprecation.Property(d => d.AlternateVersionRange).HasMaxLength(MaxPackageDependencyVersionRangeLength);

        // A version has at most one deprecation. The relationship is optional in the
        // database for the same reason as the dependency one: a required relationship
        // would need a foreign key change, which SQLite cannot make in place.
        // The foreign key is declared explicitly: for a one to one relationship EF
        // cannot otherwise tell which side is the dependent.
        deprecation
            .HasOne(d => d.Package)
            .WithOne(p => p.Deprecation)
            .HasForeignKey<PackageDeprecation>(d => d.PackageKey)
            .IsRequired(false);
    }

    private void BuildPackageVulnerabilityEntity(EntityTypeBuilder<PackageVulnerability> vulnerability)
    {
        vulnerability.HasKey(v => v.Key);

        vulnerability.Property(v => v.AdvisoryUrl).HasMaxLength(DefaultMaxStringLength);
        vulnerability.Property(v => v.VersionRange).HasMaxLength(MaxPackageDependencyVersionRangeLength);

        vulnerability
            .HasOne(v => v.Package)
            .WithMany(p => p.Vulnerabilities)
            .HasForeignKey(v => v.PackageKey)
            .IsRequired(false);
    }

    private void BuildCatalogEntryEntity(EntityTypeBuilder<CatalogEntry> entry)
    {
        entry.HasKey(e => e.Key);
        entry.HasIndex(e => e.CommitId).IsUnique();
        entry.HasIndex(e => e.RepositoryKey);

        entry.Property(e => e.CommitId).HasMaxLength(MaxCatalogCommitIdLength).IsRequired();
        entry.Property(e => e.Type).HasMaxLength(MaxCatalogTypeLength).IsRequired();
        entry.Property(e => e.PackageId).HasMaxLength(MaxPackageIdLength).IsRequired();
        entry.Property(e => e.PackageVersion).HasMaxLength(MaxPackageVersionLength).IsRequired();
    }

    private void BuildOrganizationEntity(EntityTypeBuilder<Organization> organization)
    {
        organization.HasKey(o => o.Key);
        organization.HasIndex(o => o.Name).IsUnique();

        organization.Property(o => o.Name).HasMaxLength(MaxAccountNameLength).IsRequired();
    }

    private void BuildOrganizationMemberEntity(EntityTypeBuilder<OrganizationMember> member)
    {
        member.HasKey(m => m.Key);
        member.HasIndex(m => m.UserId);

        member.Property(m => m.UserId).HasMaxLength(MaxUserIdLength).IsRequired();

        member
            .HasOne(m => m.Organization)
            .WithMany()
            .HasForeignKey(m => m.OrganizationKey)
            .IsRequired(false);
    }

    private void BuildPackageOwnerEntity(EntityTypeBuilder<PackageOwner> owner)
    {
        owner.HasKey(o => o.Key);
        owner.HasIndex(o => new { o.RepositoryKey, o.PackageId });
        owner.HasIndex(o => o.OwnerName);

        owner.Property(o => o.PackageId).HasMaxLength(MaxPackageIdLength).IsRequired();
        owner.Property(o => o.OwnerName).HasMaxLength(MaxAccountNameLength).IsRequired();
    }

    private void BuildApiKeyEntity(EntityTypeBuilder<ApiKey> apiKey)
    {
        apiKey.HasKey(k => k.Key);
        apiKey.HasIndex(k => k.HashedKey).IsUnique();
        apiKey.HasIndex(k => k.UserId);

        apiKey.Property(k => k.UserId).HasMaxLength(MaxUserIdLength).IsRequired();
        apiKey.Property(k => k.Description).HasMaxLength(MaxApiKeyDescriptionLength);
        apiKey.Property(k => k.HashedKey).HasMaxLength(MaxApiKeyHashLength).IsRequired();
        apiKey.Property(k => k.PackageGlobPattern).HasMaxLength(MaxPackageIdLength);

        #pragma warning disable CS8620
        apiKey
            .Property(k => k.Scopes)
            .HasMaxLength(DefaultMaxStringLength)
            .HasConversion(StringArrayToJsonConverter.Instance)
            .Metadata.SetValueComparer(StringArrayComparer.Instance);
        #pragma warning restore CS8620
    }

    private void BuildPackageValidationEntity(EntityTypeBuilder<PackageValidation> validation)
    {
        validation.HasKey(v => v.Key);
        validation.HasIndex(v => v.SubmissionId);
        validation.HasIndex(v => new { v.RepositoryKey, v.PackageId });

        validation.Property(v => v.SubmissionId).HasMaxLength(MaxCatalogCommitIdLength).IsRequired();
        validation.Property(v => v.PackageId).HasMaxLength(MaxPackageIdLength).IsRequired();
        validation.Property(v => v.PackageVersion).HasMaxLength(MaxPackageVersionLength).IsRequired();
        validation.Property(v => v.Type).HasMaxLength(MaxValidationTypeLength).IsRequired();
        validation.Property(v => v.Message).HasMaxLength(DefaultMaxStringLength);

        validation
            .HasOne(v => v.Package)
            .WithMany()
            .HasForeignKey(v => v.PackageKey)
            .IsRequired(false);
    }

    private void BuildAbuseReportEntity(EntityTypeBuilder<AbuseReport> report)
    {
        report.HasKey(r => r.Key);
        report.HasIndex(r => new { r.RepositoryKey, r.PackageId });

        report.Property(r => r.PackageId).HasMaxLength(MaxPackageIdLength).IsRequired();
        report.Property(r => r.PackageVersion).HasMaxLength(MaxPackageVersionLength).IsRequired();
        report.Property(r => r.Reason).HasMaxLength(MaxAbuseReasonLength).IsRequired();
        report.Property(r => r.Details).HasMaxLength(DefaultMaxStringLength).IsRequired();
        report.Property(r => r.ReporterEmail).HasMaxLength(MaxEmailLength);
    }

    private void BuildPackageDependencyEntity(EntityTypeBuilder<PackageDependency> dependency)
    {
        dependency.HasKey(d => d.Key);
        dependency.HasIndex(d => d.Id);

        dependency.Property(d => d.Id).HasMaxLength(MaxPackageIdLength);
        dependency.Property(d => d.VersionRange).HasMaxLength(MaxPackageDependencyVersionRangeLength);
        dependency.Property(d => d.TargetFramework).HasMaxLength(MaxTargetFrameworkLength);
    }

    private void BuildPackageTypeEntity(EntityTypeBuilder<PackageType> type)
    {
        type.HasKey(d => d.Key);
        type.HasIndex(d => d.Name);

        type.Property(d => d.Name).HasMaxLength(MaxPackageTypeNameLength);
        type.Property(d => d.Version).HasMaxLength(MaxPackageTypeVersionLength);
    }

    private void BuildTargetFrameworkEntity(EntityTypeBuilder<TargetFramework> targetFramework)
    {
        targetFramework.HasKey(f => f.Key);
        targetFramework.HasIndex(f => f.Moniker);

        targetFramework.Property(f => f.Moniker).HasMaxLength(MaxTargetFrameworkLength);
    }
}
