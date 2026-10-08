using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins the warning lines curl 8.21.0 prints while reading a command line it carries on with,
/// byte for byte (measured with the local curl on 2026-09-26).
/// </summary>
[TestClass]
public sealed class CommandLineWarningTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void FileNameLooksLikeFlag_Value_IsCurlsExactText()
    {
        Diagnostics.Arrange("file name", "\"-s\"");
        string line = CommandLineWarning.FileNameLooksLikeFlag("-s");
        Diagnostics.Act("warning", line);

        Diagnostics.Assert("warning", "Warning: The filename argument '-s' looks like a flag.", line);
        Assert.AreEqual("Warning: The filename argument '-s' looks like a flag.", line);
    }

    [TestMethod]
    public void FileNameLooksLikeFlag_Null_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("file name", "null");
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineWarning.FileNameLooksLikeFlag(null!));
        Diagnostics.Act("exception", exception.GetType().Name);

        Diagnostics.Assert("parameter name", "fileName", exception.ParamName);
        Assert.AreEqual("fileName", exception.ParamName);
    }

    [TestMethod]
    public void MoreOutputOptionsThanUrls_IsCurlsExactText()
    {
        Diagnostics.Arrange("warning", nameof(CommandLineWarning.MoreOutputOptionsThanUrls));
        Diagnostics.Act("warning", CommandLineWarning.MoreOutputOptionsThanUrls);
        Diagnostics.Assert("warning", "Warning: Got more output options than URLs", CommandLineWarning.MoreOutputOptionsThanUrls);
        Assert.AreEqual("Warning: Got more output options than URLs", CommandLineWarning.MoreOutputOptionsThanUrls);
    }

    [TestMethod]
    public void TimeConditionIsNotADate_IsCurlsExactUnwrappedLine()
    {
        AssertLines(
            nameof(CommandLineWarning.TimeConditionIsNotADate),
            CommandLineWarning.TimeConditionIsNotADate,
            "Warning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax.");
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax.",
            },
            CommandLineWarning.TimeConditionIsNotADate.ToArray());
    }

    [TestMethod]
    public void PostRequestedWithHead_IsCurlsExactUnwrappedLine()
    {
        AssertLines(
            nameof(CommandLineWarning.PostRequestedWithHead),
            CommandLineWarning.PostRequestedWithHead,
            "Warning: You can only select one HTTP request method! You asked for both POST (-d, --data) and HEAD (-I, --head).");
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: You can only select one HTTP request method! You asked for both POST (-d, --data) and HEAD (-I, --head).",
            },
            CommandLineWarning.PostRequestedWithHead.ToArray());
    }

    [TestMethod]
    public void PostRequestedWithGet_IsCurlsExactUnwrappedLine()
    {
        AssertLines(
            nameof(CommandLineWarning.PostRequestedWithGet),
            CommandLineWarning.PostRequestedWithGet,
            "Warning: You can only select one HTTP request method! You asked for both POST (-d, --data) and GET (-G, --get).");
        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: You can only select one HTTP request method! You asked for both POST (-d, --data) and GET (-G, --get).",
            },
            CommandLineWarning.PostRequestedWithGet.ToArray());
    }

    /// <summary>Writes the warning's name, its lines and the expected lines as diagnostics.</summary>
    private void AssertLines(string warningName, IEnumerable<string> actualLines, params string[] expectedLines)
    {
        Diagnostics.Arrange("warning", warningName);
        Diagnostics.Act("lines", CommandLineParseDiagnostics.QuoteEach(actualLines));
        Diagnostics.Assert("lines", CommandLineParseDiagnostics.QuoteEach(expectedLines), CommandLineParseDiagnostics.QuoteEach(actualLines));
    }
}
