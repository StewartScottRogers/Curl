using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.StructuredFields;

/// <summary>
/// The <c>--structured-fields</c> transfer hook (ADR-1149). After the transfer it parses every known Structured
/// Field in the response (<see cref="KnownStructuredFields" />, RFC 9651 §4.2), reports each as the <c>-v</c> line
/// <c>* Structured field &lt;name&gt;: &lt;readable value&gt;</c>, and gives <c>-w</c> the variables
/// <c>structured_fields</c> (every field, one <c>&lt;name&gt;: &lt;readable value&gt;</c> line each) and
/// <c>structured_field.&lt;lowercase name&gt;</c> (one field's readable value). It sends nothing and changes no
/// output curl writes.
/// </summary>
internal sealed class StructuredFieldsTransferHook : ITransferHook
{
    /// <summary>The <c>-w</c> variable that holds every known field's readable line.</summary>
    public const string AllFieldsVariable = "structured_fields";

    /// <summary>The prefix of the <c>-w</c> variable that holds one field's readable value.</summary>
    public const string FieldVariablePrefix = "structured_field.";

    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context, Func<ITransferContext, ValueTask<TransferResult>> next)
    {
        var result = await next(context);
        if (result.Report is not { } report)
        {
            return result;
        }

        var fields = DescribedFields(report.ResponseHeaders);
        Dictionary<string, string> values = new(report.ExtensionWriteOutValues)
        {
            [AllFieldsVariable] = string.Join("\n", fields.Select(field => field.Name + ": " + field.Text)),
        };
        foreach (var (name, text) in fields)
        {
            context.Events.ReportInfo($"Structured field {name}: {text}");
            values[FieldVariablePrefix + name.ToLowerInvariant()] = text;
        }

        return result with { Report = report with { ExtensionWriteOutValues = values } };
    }

    private static List<(string Name, string Text)> DescribedFields(IReadOnlyList<KeyValuePair<string, string>> headers) =>
        [.. headers
            .Where(header => KnownStructuredFields.Types.ContainsKey(header.Key))
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .Select(field => (field.Key, KnownStructuredFields.Describe(
                string.Join(", ", field.Select(line => line.Value.Trim(' ', '\t'))),
                KnownStructuredFields.Types[field.Key])))];
}
