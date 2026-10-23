using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance.Differential.Libcurl.Mime;

/// <summary>
/// Compares two libraries' result-code logs and wire logs for one MIME case as the tester's
/// <c>mimetest.py</c> did: the multipart boundary (15 or more dashes and 8 to 32 letters and
/// digits) is blanked in each wire log; differing logs are reported in full (cut to 300
/// characters) and differing wire logs from 60 bytes before their first difference to 140 after.
/// </summary>
public static class LibcurlMimeComparer
{
    private const int CodesReported = 300;

    private const int WireBefore = 60;

    private const int WireAfter = 140;

    private static readonly Regex Boundary = new("-{15,}[A-Za-z0-9]{8,32}", RegexOptions.CultureInvariant);

    /// <summary>Compares one case's runs.</summary>
    /// <param name="name">The case's name.</param>
    /// <param name="reference">The reference libcurl's result-code log.</param>
    /// <param name="ours">Curl's Libcurl result-code log.</param>
    /// <param name="referenceWire">The loopback server's log of the reference run.</param>
    /// <param name="oursWire">The loopback server's log of Curl's run.</param>
    /// <returns>The outcome.</returns>
    public static LibcurlMimeCaseOutcome Compare(string name, string reference, string ours, byte[] referenceWire, byte[] oursWire)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(ours);
        var differences = new List<string>();
        if (reference != ours)
        {
            differences.Add("  CODES OURS " + Cut(ours));
            differences.Add("  CODES REAL " + Cut(reference));
        }

        var realWire = NormaliseWire(referenceWire);
        var ourWire = NormaliseWire(oursWire);
        if (realWire != ourWire)
        {
            var first = FirstDifference(ourWire, realWire);
            differences.Add(string.Create(CultureInfo.InvariantCulture, $"  WIRE first diff at {first} of {ourWire.Length} {realWire.Length}"));
            differences.Add("   OURS " + Around(ourWire, first));
            differences.Add("   REAL " + Around(realWire, first));
        }

        return new LibcurlMimeCaseOutcome(name, differences);
    }

    /// <summary>Blanks the multipart boundary in a wire log.</summary>
    /// <param name="wire">The wire log.</param>
    /// <returns>The log as Latin-1 text with every boundary replaced by <c>&lt;BOUNDARY&gt;</c>.</returns>
    public static string NormaliseWire(byte[] wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        return Boundary.Replace(Encoding.Latin1.GetString(wire), "<BOUNDARY>");
    }

    private static int FirstDifference(string ours, string reference)
    {
        var shorter = Math.Min(ours.Length, reference.Length);
        var index = 0;
        while (index < shorter && ours[index] == reference[index])
        {
            index++;
        }

        return index;
    }

    private static string Around(string wire, int first)
    {
        var start = Math.Max(first - WireBefore, 0);
        var end = Math.Min(first + WireAfter, wire.Length);
        return PythonLiteral.FormatBytes(Encoding.Latin1.GetBytes(wire[start..end]));
    }

    private static string Cut(string log) => log.Length > CodesReported ? log[..CodesReported] : log;
}
