using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>
/// An option document's header block between its two <c>---</c> lines, read as <c>single</c> in upstream's
/// <c>scripts/managen</c> reads it, with the script's checks for the fields every option must have.
/// </summary>
internal sealed class ManualOptionHeader
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly (Regex Pattern, Func<ManualOptionHeader, string, bool> Apply)[] Fields =
    [
        (Field("^Short: *(.)"), Set((header, value) => header.Short = value)),
        (Field("^Long: *(.*)"), Set((header, value) => header.Long = value)),
        (Field("^Added: *(.*)"), Set((header, value) => header.Added = value)),
        (Field("^Tags: *(.*)"), Set((_, _) => { })),
        (Field("^Arg: *(.*)"), Set((header, value) => header.Arg = value)),
        (Field("^Magic: *(.*)"), Set((_, _) => { })),
        (Field("^Mutexed: *(.*)"), Set((header, value) => header.Mutexed = value)),
        (Field("^Protocols: *(.*)"), Set((header, value) => header.Protocols = value)),
        (Field("^See-also: +(.+)"), Set((header, value) => header.AddInlineSeeAlso(value))),
        (Field("^See-also:"), Set((header, _) => header.list = 2)),
        (Field("^  *- (.*)"), (header, value) => header.AddListItem(2, header.SeeAlso, value)),
        (Field("^Requires: *(.*)"), Set((header, value) => header.Requires = value)),
        (Field("^Category: *(.*)"), Set((header, value) => header.Category = value)),
        (Field("^Example: +(.+)"), Set((header, value) => header.Examples.Add(value))),
        (Field("^Example:"), Set((header, _) => header.list = 1)),
        (Field("^  *- (.*)"), (header, value) => header.AddListItem(1, header.Examples, value)),
        (Field("^Multi: *(.*)"), Set((header, value) => header.Multi = value)),
        (Field("^Scope: *(.*)"), Set((header, value) => header.Scope = value)),
        (Field("^Experimental: yes"), Set((header, _) => header.Experimental = true)),
        (Field("^C: (.*)"), Set((header, value) => header.Copyright = value)),
        (Field("^SPDX-License-Identifier: (.*)"), Set((header, value) => header.Spdx = value)),
        (Field("^Help: *(.*)"), Set((_, _) => { })),
    ];

    private int list;
    private bool duplicateSeeAlso;

    public string? Short { get; private set; }

    public string Long { get; private set; } = "";

    public string? Added { get; private set; }

    public string? Arg { get; private set; }

    public string? Mutexed { get; private set; }

    public string? Protocols { get; private set; }

    public string? Requires { get; private set; }

    public string Category { get; private set; } = "";

    public string Multi { get; private set; } = "";

    public string? Scope { get; private set; }

    public bool Experimental { get; private set; }

    public string? Copyright { get; private set; }

    public string? Spdx { get; private set; }

    public List<string> SeeAlso { get; } = [];

    public List<string> Examples { get; } = [];

    /// <summary>The line of the closing <c>---</c>, which the script's messages name.</summary>
    public int LineNumber { get; private set; }

    /// <summary>The index of the first body line.</summary>
    public int BodyStart { get; private set; }

    /// <summary>Reads the header, writing the script's warnings and errors.</summary>
    /// <param name="context">The run.</param>
    /// <param name="fileName">The document's name.</param>
    /// <param name="lines">The document's lines.</param>
    /// <returns>The header, or the code the script returns for a header it refuses.</returns>
    public static (ManualOptionHeader Header, int? Failure) Parse(ManualPageContext context, string fileName, IReadOnlyList<string> lines)
    {
        ManualOptionHeader header = new();
        bool started = false;
        for (int index = 0; index < lines.Count; index++)
        {
            string text = lines[index];
            header.LineNumber = index + 1;
            if (Regex.IsMatch(text, "^ *<!--", Options) || !started)
            {
                started = started || text.StartsWith("---", StringComparison.Ordinal);
                continue;
            }

            if (text.StartsWith("---", StringComparison.Ordinal))
            {
                header.BodyStart = index + 1;
                return (header, header.Check(context, fileName));
            }

            if (header.ApplyField(context, fileName, text) is { } failure)
            {
                return (header, failure);
            }
        }

        context.Error.Append($"{fileName}:1:1:ERROR: no proper meta-data header\n");
        return (header, 2);
    }

    private int? ApplyField(ManualPageContext context, string fileName, string text)
    {
        if (!Array.Exists(Fields, field => field.Pattern.Match(text) is { Success: true } match && field.Apply(this, match.Groups[^1].Value)))
        {
            context.Error.Append($"{fileName}:{LineNumber}:1:WARN: unrecognized line in {fileName}, ignoring: '{ManualPerlValues.Chomp(text)}';\n");
        }

        if (duplicateSeeAlso)
        {
            context.Error.Append($"ERROR: duplicated See-also in {fileName}\n");
            return 1;
        }

        return null;
    }

    private int? Check(ManualPageContext context, string fileName)
    {
        string at = $"{fileName}:{LineNumber}:1:ERROR:";
        (bool Missing, string Message, int Code)[] checks =
        [
            (!ManualPerlValues.IsTrue(Long), $"ERROR: no 'Long:' in {fileName}", 1),
            (!ManualPerlValues.IsTrue(Category), $"ERROR: no 'Category:' in {fileName}", 2),
            (Examples.Count == 0 || !ManualPerlValues.IsTrue(Examples[0]), $"{at} no 'Example:' present", 2),
            (!ManualPerlValues.IsTrue(Added), $"{at} no 'Added:' version present", 2),
            (SeeAlso.Count == 0 || !ManualPerlValues.IsTrue(SeeAlso[0]), $"{at} no 'See-also:' field present", 2),
            (!ManualPerlValues.IsTrue(Copyright), $"{at} no 'C:' field present", 2),
            (!ManualPerlValues.IsTrue(Spdx), $"{at} no 'SPDX-License-Identifier:' field present", 2),
        ];
        foreach ((bool missing, string message, int code) in checks)
        {
            if (missing)
            {
                context.Error.Append(message).Append('\n');
                return code;
            }
        }

        return null;
    }

    private void AddInlineSeeAlso(string value)
    {
        duplicateSeeAlso = SeeAlso.Count > 0;
        SeeAlso.Add(value);
    }

    private bool AddListItem(int wanted, List<string> items, string value)
    {
        if (list != wanted)
        {
            return false;
        }

        items.Add(value);
        return true;
    }

    private static Regex Field(string pattern) => new(pattern, Options);

    private static Func<ManualOptionHeader, string, bool> Set(Action<ManualOptionHeader, string> apply) => (header, value) =>
    {
        apply(header, value);
        return true;
    };
}
