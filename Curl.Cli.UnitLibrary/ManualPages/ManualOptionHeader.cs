using System.Text.RegularExpressions;

namespace Curl.Cli.ManualPages;

/// <summary>
/// An option document's header block between its two <c>---</c> lines, read as <c>single</c> in upstream's
/// <c>scripts/managen</c> reads it, with the script's checks for the fields every option must have.
/// </summary>
public sealed class ManualOptionHeader
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
    private bool started;

    /// <summary>The one-letter short name (<c>Short:</c>), or <see langword="null"/> when the option has none.</summary>
    public string? Short { get; private set; }

    /// <summary>The long name (<c>Long:</c>).</summary>
    public string Long { get; private set; } = "";

    /// <summary>The curl version that added the option (<c>Added:</c>).</summary>
    public string? Added { get; private set; }

    /// <summary>The argument the option takes (<c>Arg:</c>).</summary>
    public string? Arg { get; private set; }

    /// <summary>The space-separated options this one excludes (<c>Mutexed:</c>).</summary>
    public string? Mutexed { get; private set; }

    /// <summary>The space-separated protocols the option applies to (<c>Protocols:</c>).</summary>
    public string? Protocols { get; private set; }

    /// <summary>The feature libcurl must be built with (<c>Requires:</c>).</summary>
    public string? Requires { get; private set; }

    /// <summary>The help categories (<c>Category:</c>).</summary>
    public string Category { get; private set; } = "";

    /// <summary>What giving the option several times does (<c>Multi:</c>).</summary>
    public string Multi { get; private set; } = "";

    /// <summary>The option's scope (<c>Scope:</c>), <c>global</c> or unset.</summary>
    public string? Scope { get; private set; }

    /// <summary>Whether the option is marked <c>Experimental: yes</c>.</summary>
    public bool Experimental { get; private set; }

    /// <summary>The copyright line (<c>C:</c>).</summary>
    public string? Copyright { get; private set; }

    /// <summary>The licence (<c>SPDX-License-Identifier:</c>).</summary>
    public string? Spdx { get; private set; }

    /// <summary>The options named under <c>See-also:</c>, inline or as a list.</summary>
    public List<string> SeeAlso { get; } = [];

    /// <summary>The example command lines under <c>Example:</c>, inline or as a list.</summary>
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
        for (int index = 0; index < lines.Count; index++)
        {
            header.LineNumber = index + 1;
            if (header.ReadLine(context, fileName, lines[index], out int? failure))
            {
                return (header, failure);
            }
        }

        context.Error.Append($"{fileName}:1:1:ERROR: no proper meta-data header\n");
        return (header, 2);
    }

    // Reads one line; true when the header ends there, with the code the script returns when it refuses the header.
    private bool ReadLine(ManualPageContext context, string fileName, string text, out int? failure)
    {
        failure = null;
        if (Regex.IsMatch(text, "^ *<!--", Options) || !started)
        {
            started = started || text.StartsWith("---", StringComparison.Ordinal);
            return false;
        }

        if (text.StartsWith("---", StringComparison.Ordinal))
        {
            BodyStart = LineNumber;
            failure = Check(context, fileName);
            return true;
        }

        failure = ApplyField(context, fileName, text);
        return failure is not null;
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
