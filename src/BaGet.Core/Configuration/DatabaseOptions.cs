using System.ComponentModel.DataAnnotations;

namespace BaGet.Core;

public class DatabaseOptions
{
    public string Type { get; set; }

    [Required]
    public string ConnectionString { get; set; }

    /// <summary>
    /// The MySQL server version, such as "8.0.0-mysql" or "10.11.0-mariadb". Optional.
    /// </summary>
    /// <remarks>
    /// When this is not set, the version is detected by connecting to the server. Detection
    /// happens while the database options are being built, which also happens when the
    /// Entity Framework design-time tools enumerate the contexts, so an unreachable server
    /// makes commands such as "dotnet ef migrations add" fail even for another provider.
    /// Set this to keep option building offline.
    /// </remarks>
    public string MySqlServerVersion { get; set; }
}