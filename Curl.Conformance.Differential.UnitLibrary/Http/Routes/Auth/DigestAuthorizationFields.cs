using System.Text.RegularExpressions;

namespace Curl.Conformance.Differential.Http.Routes.Auth;

/// <summary>
/// The <c>name=value</c> fields of a Digest <c>Authorization</c> header as server.py's digest routes read
/// them: a quoted value without its quotes, an unquoted value up to a comma or white space, and the last
/// field of one name winning.
/// </summary>
public static class DigestAuthorizationFields
{
    // Interpreted, not [GeneratedRegex]: generated code would count against this library's coverage gate.
    private const string FieldPattern = """(\w+)=(?:"([^"]*)"|([^,\s]*))""";

    /// <summary>Reads every field of <paramref name="authorization"/> by name.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string authorization)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(authorization, FieldPattern))
        {
            fields[match.Groups[1].Value] = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
        }

        return fields;
    }
}
