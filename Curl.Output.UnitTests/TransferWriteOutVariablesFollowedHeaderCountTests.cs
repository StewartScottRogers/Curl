using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <c>%{num_headers}</c> after a followed redirect hop with no response: curl 8.21.0 prints the
/// followed response's count while <c>%{header_json}</c> is <c>{}</c> (measured 2026-11-28, BL-6096).
/// </summary>
[TestClass]
public sealed class TransferWriteOutVariablesFollowedHeaderCountTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryGetVariableText_FollowedHeaderCount_IsPrintedWithNoHeaders()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        TransferResult result = TransferResult.Failure(CurlExitCode.CouldntConnect, "Could not connect to server", 0)
            with
        { Report = new TransferReport { FollowedHeaderCount = 4 } };
        diagnostics.Arrange("followed header count", 4);
        TransferWriteOutVariables variables = new(new(), result, "http://example.test/a", 0);

        bool counted = variables.TryGetVariableText("num_headers", out string? count);
        bool formatted = variables.TryGetVariableText("header_json", out string? json);

        diagnostics.Act("num_headers", count);
        diagnostics.Act("header_json", json);
        Assert.IsTrue(counted);
        Assert.AreEqual("4", count);
        Assert.IsTrue(formatted);
        Assert.AreEqual("{}", json?.ReplaceLineEndings(string.Empty));
    }
}
