using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace BaGet.Tests.Contracts;

/// <summary>
/// The work of the UI API split that is still to do, as two text files next to the contract snapshots: the screens that have no
/// <c>[UiApi]</c> twin yet (<c>ui-api-pending-screens.txt</c>) and the <c>*Workflow</c> types that still live in the UI projects
/// (<c>ui-api-pending-workflows.txt</c>). The checks are one-directional: what is pending now must be listed (nothing may be added to
/// the old way), while a stale line is harmless, so parallel tracks never conflict on these files. A track deletes the lines of what it
/// moved; the finishing track empties both files and the tests then demand that nothing is pending.
/// </summary>
internal static class UiApiPending
{
    public const string ScreensFile = "ui-api-pending-screens.txt";
    public const string WorkflowsFile = "ui-api-pending-workflows.txt";

    /// <summary>Asserts that every entry of <paramref name="actual"/> is listed in <paramref name="fileName"/>.</summary>
    public static void VerifyListed(string fileName, IReadOnlyCollection<string> actual, string what)
    {
        var path = Path.Combine(ContractSnapshot.Folder, fileName);

        if (Environment.GetEnvironmentVariable(ContractSnapshot.UpdateVariable) == "1")
        {
            File.WriteAllText(path, string.Join("\n", actual.OrderBy(l => l, StringComparer.Ordinal)) + "\n", new UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(path), $"{fileName} does not exist; generate it with {ContractSnapshot.UpdateVariable}=1.");
        var listed = File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0).ToHashSet(StringComparer.Ordinal);
        var unlisted = actual.Where(a => !listed.Contains(a)).OrderBy(a => a, StringComparer.Ordinal).ToList();

        Assert.True(
            unlisted.Count == 0,
            $"{what} that {fileName} does not list (new work must follow the split: use cases in the Application project, a [UiApi] twin per screen):{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", unlisted));
    }
}
