using System.Diagnostics;
using OpenTelemetry;

namespace BaGet.Telemetry;

/// <summary>
/// Removes the SQL text from finished spans. <c>OpenTelemetry.Instrumentation.EntityFrameworkCore</c> offers no switch
/// for it and always records the (literal-sanitized) command as <c>db.query.text</c> (<c>db.statement</c> under the
/// older conventions): table and column names and the query shape do not belong in a trace backend by default. It must be
/// added before the exporting processor so the tags are gone when the batch is queued.
/// </summary>
internal sealed class SqlTextRedactingProcessor : BaseProcessor<Activity>
{
    /// <summary>The tags that hold SQL text.</summary>
    internal static readonly string[] Tags = { "db.query.text", "db.statement" };

    public override void OnEnd(Activity data)
    {
        foreach (var tag in Tags)
        {
            // Setting a null value removes the tag.
            if (data.GetTagItem(tag) != null) data.SetTag(tag, null);
        }
    }
}
