using System.Security.Cryptography;
using System.Text;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CurlOptionManualSection"/> to curl 8.21.0's <c>--help &lt;option&gt;</c>, measured on
/// 2026-09-27 with the mingw reference build (Git for Windows' <c>mingw64\bin\curl.exe</c>) writing to a
/// redirected standard output. <c>help-option-&lt;name&gt;.txt</c> under <c>HelpReference</c> is one section, its
/// CR LF line ends made LF. <c>help-option-measurements.txt</c> holds one line per subject - every long name in
/// curl's option list as <c>--name</c> and <c>--no-name</c>, every letter as <c>-x</c>, and some that name no
/// option - with the byte count and SHA-256 of the standard output, and <c>incorrect</c> when standard error
/// held the Incorrect-option line (<c>-</c> when it was empty). curl exited 0 for every subject.
/// </summary>
[TestClass]
public sealed class CurlOptionManualSectionTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("-v", "help-option-v.txt")]
    [DataRow("--verbose", "help-option-v.txt")]
    [DataRow("--no-verbose", "help-option-v.txt")]
    [DataRow("--xattr", "help-option-xattr.txt")]
    [DataRow("--cert-status", "help-option-cert-status.txt")]
    [DataRow("--keepalive", "help-option-keepalive.txt")]
    [DataRow("--no-keepalive", "help-option-keepalive.txt")]
    public void TryGetLines_KnownOption_IsTheMeasuredSection(string subject, string referenceFile)
    {
        Diagnostics.Arrange("reference file", referenceFile);
        bool found = TryGetLines(subject, out IReadOnlyList<string> lines);

        string[] expected = ReferenceLines(referenceFile);
        Diagnostics.Assert("found", true, found);
        Diagnostics.Assert("lines", CommandLineParseDiagnostics.QuoteEach(expected), CommandLineParseDiagnostics.QuoteEach(lines));
        Assert.IsTrue(found);
        CollectionAssert.AreEqual(ReferenceLines(referenceFile), lines.ToArray());
    }

    [TestMethod]
    public void TryGetLines_Verbose_StartsWithItsHeadingAndStopsBeforeTheNextOption()
    {
        TryGetLines("-v", out IReadOnlyList<string> lines);

        Diagnostics.Assert("first line", "    -v, --verbose", lines[0]);
        Diagnostics.Assert("has a -V heading", false, lines.Any(line => line.StartsWith("    -V", StringComparison.Ordinal)));
        Assert.AreEqual("    -v, --verbose", lines[0]);
        Assert.IsFalse(lines.Any(line => line.StartsWith("    -V", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("--bogus")]
    [DataRow("--")]
    [DataRow("-")]
    [DataRow("--no-output")]
    [DataRow("--no-")]
    [DataRow("--no-no-buffer")]
    [DataRow("--VERBOSE")]
    [DataRow("--verb")]
    [DataRow("-vv")]
    [DataRow("-?")]
    [DataRow("- ")]
    public void TryGetLines_NoSuchOption_IsFalseWithNoLines(string subject)
    {
        bool found = TryGetLines(subject, out IReadOnlyList<string> lines);

        Diagnostics.Assert("found", false, found);
        Diagnostics.Assert("line count", 0, lines.Count);
        Assert.IsFalse(found);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    [DataRow("--no-compressed")]
    [DataRow("--include")]
    public void TryGetLines_OptionTheManualHasNoHeadingFor_IsTrueWithNoLines(string subject)
    {
        bool found = TryGetLines(subject, out IReadOnlyList<string> lines);

        Diagnostics.Assert("found", true, found);
        Diagnostics.Assert("line count", 0, lines.Count);
        Assert.IsTrue(found);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void IncorrectOptionNameMessage_IsCurls()
    {
        Diagnostics.Arrange("message", nameof(CurlOptionManualSection.IncorrectOptionNameMessage));
        string message = CurlOptionManualSection.IncorrectOptionNameMessage;
        Diagnostics.Act("message", message);

        Diagnostics.Assert("message", "Incorrect option name to show help for, see curl -h", message);
        Assert.AreEqual("Incorrect option name to show help for, see curl -h", message);
    }

    [TestMethod]
    public void TryGetLines_EveryMeasuredSubject_GivesTheMeasuredBytes()
    {
        string[] measurements = ReferenceLines("help-option-measurements.txt");
        Diagnostics.Arrange("measured subjects", measurements.Length);
        List<string> mismatches = [];
        foreach (string measurement in measurements)
        {
            string[] fields = measurement.Split('\t');
            bool found = CurlOptionManualSection.TryGetLines(fields[0], out IReadOnlyList<string> lines);
            byte[] bytes = Encoding.ASCII.GetBytes(string.Concat(lines.Select(line => line + "\r\n")));
            string actual = $"{fields[0]}\t{bytes.Length}\t{Convert.ToHexStringLower(SHA256.HashData(bytes))}\t{(found ? "-" : "incorrect")}";
            if (actual != measurement)
            {
                mismatches.Add(actual);
            }
        }

        Diagnostics.Act("mismatches", CommandLineParseDiagnostics.QuoteEach(mismatches));
        Diagnostics.Assert("measured subjects", 634, measurements.Length);
        Diagnostics.Assert("mismatch count", 0, mismatches.Count);
        Assert.HasCount(634, measurements);
        Assert.IsEmpty(mismatches, string.Join("\n", mismatches));
    }

    private static string[] ReferenceLines(string referenceFile)
    {
        using Stream stream = typeof(CurlOptionManualSectionTests).Assembly.GetManifestResourceStream("HelpReference." + referenceFile)!;
        using StreamReader reader = new(stream);
        string text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        return text.Split('\n')[..^1];
    }

    /// <summary>Calls <see cref="CurlOptionManualSection.TryGetLines"/>, writing the subject, whether it was found and the lines as diagnostics.</summary>
    private bool TryGetLines(string subject, out IReadOnlyList<string> lines)
    {
        Diagnostics.Arrange("subject", "\"" + subject + "\"");
        bool found = CurlOptionManualSection.TryGetLines(subject, out lines);
        Diagnostics.Act("found", found);
        Diagnostics.Act("lines", CommandLineParseDiagnostics.QuoteEach(lines));
        return found;
    }
}
