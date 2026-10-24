using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>
/// Compares two libraries' round lines and wire logs for one case as the tester's
/// <c>easytest.py</c> did: a round differs when its lines differ after the elapsed time in an
/// error text and the <c>CURLINFO_PRIMARY_PORT</c> value (<c>200028</c>) are blanked, and is
/// reported as its first four differing <c> |</c> segments; a differing round count or wire
/// log (digest <c>cnonce</c> and <c>response</c> blanked) is reported too.
/// </summary>
public static class LibcurlEasyComparer
{
    private const int SegmentsReported = 4;

    private const int WireBytesReported = 500;

    private const int LineLimit = 900;

    private static readonly Regex ErrorText = new("err=([0-9A-F]*)", RegexOptions.CultureInvariant);

    private static readonly Regex Elapsed = new(@"after \d+ ms|after \d+ milliseconds", RegexOptions.CultureInvariant);

    private static readonly Regex PrimaryPort = new(@"\|200028=0:\d+", RegexOptions.CultureInvariant);

    private static readonly Regex ClientNonce = new("cnonce=\"[^\"]*\"", RegexOptions.CultureInvariant);

    private static readonly Regex DigestResponse = new("response=\"[^\"]*\"", RegexOptions.CultureInvariant);

    /// <summary>Compares one case's runs.</summary>
    /// <param name="name">The case's name.</param>
    /// <param name="reference">The reference libcurl's round lines.</param>
    /// <param name="ours">Curl's Libcurl round lines.</param>
    /// <param name="referenceWire">The loopback server's log of the reference run.</param>
    /// <param name="oursWire">The loopback server's log of Curl's run.</param>
    /// <returns>The outcome.</returns>
    public static LibcurlEasyCaseOutcome Compare(string name, IReadOnlyList<string> reference, IReadOnlyList<string> ours, byte[] referenceWire, byte[] oursWire)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(ours);
        var differences = reference.Zip(ours)
            .Where(pair => Strip(pair.First) != Strip(pair.Second))
            .Select(pair => DifferingSegments(pair.First, pair.Second))
            .ToList();
        if (reference.Count != ours.Count)
        {
            differences.Add(string.Create(CultureInfo.InvariantCulture, $"['ROUNDS', {reference.Count}, {ours.Count}]"));
        }

        var realWire = NormaliseWire(referenceWire);
        var ourWire = NormaliseWire(oursWire);
        if (!realWire.AsSpan().SequenceEqual(ourWire))
        {
            differences.Add(Cut($"['WIRE', {Head(realWire)}, {Head(ourWire)}]"));
        }

        return new LibcurlEasyCaseOutcome(name, differences);
    }

    /// <summary>Blanks what may differ between two runs of the same round.</summary>
    /// <param name="line">A round line.</param>
    /// <returns>The line with elapsed times in the error text and the <c>200028</c> value blanked.</returns>
    public static string Strip(string line)
    {
        var errorBlanked = ErrorText.Replace(line, match =>
        {
            var text = Encoding.Latin1.GetString(Convert.FromHexString(match.Groups[1].Value));
            return "err=" + Convert.ToHexString(Encoding.Latin1.GetBytes(Elapsed.Replace(text, "after N ms")));
        });
        return PrimaryPort.Replace(errorBlanked, "|200028=0:P");
    }

    /// <summary>Blanks the digest <c>cnonce</c> and <c>response</c> in a wire log.</summary>
    /// <param name="wire">The wire log.</param>
    /// <returns>The log with both blanked.</returns>
    public static byte[] NormaliseWire(byte[] wire)
    {
        ArgumentNullException.ThrowIfNull(wire);
        var text = Encoding.Latin1.GetString(wire);
        text = DigestResponse.Replace(ClientNonce.Replace(text, "cnonce=\"X\""), "response=\"X\"");
        return Encoding.Latin1.GetBytes(text);
    }

    private static string DifferingSegments(string reference, string ours)
    {
        var pairs = reference.Split(" |").Zip(ours.Split(" |"))
            .Where(pair => pair.First != pair.Second)
            .Take(SegmentsReported)
            .Select(pair => $"({PythonLiteral.FormatString(pair.First)}, {PythonLiteral.FormatString(pair.Second)})");
        return Cut($"[{string.Join(", ", pairs)}]");
    }

    private static string Head(byte[] wire) => PythonLiteral.FormatBytes(wire.AsSpan(0, Math.Min(wire.Length, WireBytesReported)));

    private static string Cut(string line) => line.Length > LineLimit ? line[..LineLimit] : line;
}
