using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace BaGet
{
    /// <summary>
    /// BaGet's options configuration, specific to the default BaGet application.
    /// Don't use this if you are embedding BaGet into your own custom ASP.NET Core application.
    /// </summary>
    public class ConfigureBaGetOptions
        : IConfigureOptions<CorsOptions>
        , IConfigureOptions<FormOptions>
        , IConfigureOptions<ForwardedHeadersOptions>
        , IConfigureOptions<HostFilteringOptions>
        , IConfigureOptions<IISServerOptions>
        , IValidateOptions<BaGetOptions>
    {
        public const string CorsPolicy = "AllowAll";

        /// <summary>
        /// The <see cref="BaGetOptions.AllowedHosts"/> entry that disables host filtering.
        /// </summary>
        public const string AllowAnyHost = "*";

        private readonly IConfiguration _configuration;

        public ConfigureBaGetOptions(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        private static readonly HashSet<string> ValidDatabaseTypes
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AzureTable",
                "MySql",
                "PostgreSql",
                "Sqlite",
                "SqlServer",
            };

        private static readonly HashSet<string> ValidStorageTypes
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AliyunOss",
                "AwsS3",
                "AzureBlobStorage",
                "Filesystem",
                "GoogleCloud",
                "Null",
            };

        private static readonly HashSet<string> ValidSearchTypes
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AzureSearch",
                "Database",
                "Null",
            };

        public void Configure(CorsOptions options)
        {
            // TODO: Consider disabling this on production builds.
            options.AddPolicy(
                CorsPolicy,
                builder => builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        }

        public void Configure(FormOptions options)
        {
            options.MultipartBodyLengthLimit = int.MaxValue;
        }

        public void Configure(ForwardedHeadersOptions options)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Do not restrict to local network/proxy
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        }

        public void Configure(HostFilteringOptions options)
        {
            var allowedHosts = ParseAllowedHosts(_configuration[nameof(BaGetOptions.AllowedHosts)]);

            options.AllowedHosts = allowedHosts;

            // An absent or empty "Host" header cannot be on an allow-list, and the URLs
            // BaGet advertises are built from that header. Only tolerate it while host
            // filtering is disabled, which is how the middleware behaves out of the box.
            options.AllowEmptyHosts = allowedHosts.Contains(AllowAnyHost);
        }

        /// <summary>
        /// Turn a configured <see cref="BaGetOptions.AllowedHosts"/> value into the list the
        /// host filtering middleware expects. An absent or blank value means "allow any
        /// host", so that a deployment which never configured this keeps working.
        /// </summary>
        internal static List<string> ParseAllowedHosts(string value)
        {
            var hosts = (value ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                // ASP.NET Core's own parsing of this key does not trim, which turns the
                // usual "a.example.com; b.example.com" spacing into patterns that match
                // nothing and locks out hosts the operator meant to allow.
                .Select(host => host.Trim())
                .Where(host => host.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (hosts.Count == 0)
            {
                return new List<string> { AllowAnyHost };
            }

            return hosts;
        }

        public void Configure(IISServerOptions options)
        {
            options.MaxRequestBodySize = 262144000;
        }

        public ValidateOptionsResult Validate(string name, BaGetOptions options)
        {
            var failures = new List<string>();

            if (options.Database == null) failures.Add($"The '{nameof(BaGetOptions.Database)}' config is required");
            if (options.Mirror == null) failures.Add($"The '{nameof(BaGetOptions.Mirror)}' config is required");
            if (options.Search == null) failures.Add($"The '{nameof(BaGetOptions.Search)}' config is required");
            if (options.Storage == null) failures.Add($"The '{nameof(BaGetOptions.Storage)}' config is required");

            if (!ValidDatabaseTypes.Contains(options.Database?.Type))
            {
                failures.Add(
                    $"The '{nameof(BaGetOptions.Database)}:{nameof(DatabaseOptions.Type)}' config is invalid. " +
                    $"Allowed values: {string.Join(", ", ValidDatabaseTypes)}");
            }

            if (!ValidStorageTypes.Contains(options.Storage?.Type))
            {
                failures.Add(
                    $"The '{nameof(BaGetOptions.Storage)}:{nameof(StorageOptions.Type)}' config is invalid. " +
                    $"Allowed values: {string.Join(", ", ValidStorageTypes)}");
            }

            if (!ValidSearchTypes.Contains(options.Search?.Type))
            {
                failures.Add(
                    $"The '{nameof(BaGetOptions.Search)}:{nameof(SearchOptions.Type)}' config is invalid. " +
                    $"Allowed values: {string.Join(", ", ValidSearchTypes)}");
            }

            if (failures.Any()) return ValidateOptionsResult.Fail(failures);

            return ValidateOptionsResult.Success;
        }
    }
}
