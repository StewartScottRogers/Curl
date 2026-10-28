using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins CGR-214 (BL-3743): <c>%{errormsg}</c> prints <see cref="TransferResult.WriteOutErrorMessage" />
/// when a result has one, as after <c>--fail-with-body</c>, and <see cref="TransferResult.ErrorMessage" /> otherwise.
/// </summary>
[TestClass]
public sealed class Cgr214Tests
{
    [TestMethod]
    public void TryGetVariableText_ResultWithWriteOutErrorMessage_PrintsIt()
    {
        TransferResult result = TransferResult.Failure(CurlExitCode.HttpReturnedError, "The requested URL returned error: 404")
            with { WriteOutErrorMessage = "HTTP response code said error" };
        TransferWriteOutVariables variables = new(result, "http://x/", 0, "http://x/", "http", TimeProvider.System);

        Assert.IsTrue(variables.TryGetVariableText("errormsg", out string? text));
        Assert.AreEqual("HTTP response code said error", text);
    }
}
