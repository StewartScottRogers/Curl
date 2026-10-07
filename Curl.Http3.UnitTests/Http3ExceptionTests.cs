using Curl.Testing;

namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3Exception" />: its error code and message.
/// </summary>
[TestClass]
public sealed class Http3ExceptionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_CarriesTheErrorCodeAndReason()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("error code", Http3ErrorCode.MissingSettings);
        diagnostics.Arrange("reason", "the control stream starts with a Data frame");

        Http3Exception failure = new(Http3ErrorCode.MissingSettings, "the control stream starts with a Data frame");

        diagnostics.Act("error code", failure.ErrorCode);
        diagnostics.Act("message", failure.Message);
        diagnostics.Assert("error code", Http3ErrorCode.MissingSettings, failure.ErrorCode);
        Assert.AreEqual(Http3ErrorCode.MissingSettings, failure.ErrorCode);
        diagnostics.Diff("message", "HTTP/3 MissingSettings: the control stream starts with a Data frame.", failure.Message);
        Assert.AreEqual("HTTP/3 MissingSettings: the control stream starts with a Data frame.", failure.Message);
    }
}
