using Curl.Cli;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Console;

/// <summary>
/// Pins how <c>--expect100-timeout</c> reaches <see cref="HttpRequestOptions.ContinueWait" />
/// through <see cref="HttpRequestOptionsMapping.FromCommandLine" />: curl 8.21.0 waited 200 ms
/// for <c>0.2</c>, 3 s for <c>3</c>, and its default one second for <c>0</c> and
/// <c>0.0001</c> and without the option (measured, BL-624 Notes).
/// </summary>
[TestClass]
public sealed class HttpContinueWaitMappingTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("0.2", 200)]
    [DataRow("3", 3000)]
    [DataRow("0", 1000)]
    [DataRow("0.0001", 1000)]
    public void FromCommandLine_Expect100Timeout_SetsTheContinueWait(string value, int expectedMilliseconds)
    {
        Diagnostics.Arrange("arguments", $"--expect100-timeout {value} {Url}");
        CommandLineParseResult result = CommandLineParser.Parse(["--expect100-timeout", value, Url]);
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);

        TimeSpan continueWait = HttpRequestOptionsMapping.FromCommandLine(result.Options).ContinueWait;
        Diagnostics.Act("continue wait", continueWait);

        Diagnostics.Assert("continue wait", TimeSpan.FromMilliseconds(expectedMilliseconds), continueWait);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), HttpRequestOptionsMapping.FromCommandLine(result.Options).ContinueWait);
    }

    [TestMethod]
    public void FromCommandLine_NoExpect100Timeout_WaitsTheDefaultOneSecond()
    {
        Diagnostics.Arrange("arguments", Url);
        CommandLineParseResult result = CommandLineParser.Parse([Url]);
        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);

        TimeSpan continueWait = HttpRequestOptionsMapping.FromCommandLine(result.Options).ContinueWait;
        Diagnostics.Act("continue wait", continueWait);

        Diagnostics.Assert("continue wait", HttpRequestOptions.DefaultContinueWait, continueWait);
        Assert.AreEqual(HttpRequestOptions.DefaultContinueWait, HttpRequestOptionsMapping.FromCommandLine(result.Options).ContinueWait);
    }
}
