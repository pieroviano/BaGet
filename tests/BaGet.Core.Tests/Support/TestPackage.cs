using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace BaGet.Core.Tests.Support
{
    /// <summary>
    /// Builds a real .nupkg in memory so the package reading code can be exercised
    /// without mocking it.
    /// </summary>
    public static class TestPackage
    {
        /// <summary>
        /// Create a package whose nuspec holds <paramref name="metadata"/> as the inner XML
        /// of its &lt;metadata&gt; element.
        /// </summary>
        public static MemoryStream Create(string metadata, IDictionary<string, string> files = null)
        {
            var nuspec = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
{metadata}
  </metadata>
</package>";

            var result = new MemoryStream();

            using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
            {
                Write(archive, "Package.nuspec", nuspec);

                foreach (var file in files ?? new Dictionary<string, string>())
                {
                    Write(archive, file.Key, file.Value);
                }
            }

            result.Position = 0;
            return result;
        }

        /// <summary>
        /// Create a package with the minimum required metadata, plus whatever extra
        /// elements are supplied.
        /// </summary>
        public static MemoryStream CreateMinimal(
            string extraMetadata = "",
            string id = "Test.Package",
            string version = "1.0.0",
            string authors = "Test Author",
            IDictionary<string, string> files = null)
        {
            return Create(
                $@"    <id>{id}</id>
    <version>{version}</version>
    <authors>{authors}</authors>
    <description>A test package.</description>
{extraMetadata}",
                files);
        }

        private static void Write(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);

            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));

            writer.Write(content);
        }
    }
}
