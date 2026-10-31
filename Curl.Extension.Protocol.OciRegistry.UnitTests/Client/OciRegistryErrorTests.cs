using System.Text;
using Curl.Extension.Protocol.OciRegistry.Http;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.OciRegistry.Client;

/// <summary>Checks the error body of OCI Distribution Specification 1.1, "Error Codes".</summary>
[TestClass]
public sealed class OciRegistryErrorTests
{
    [TestMethod]
    [DataRow("""{"errors":[{"code":"MANIFEST_UNKNOWN","message":"manifest unknown"}]}""", "MANIFEST_UNKNOWN", "manifest unknown")]
    [DataRow("""{"errors":[{"code":"BLOB_UNKNOWN","message":7}]}""", "BLOB_UNKNOWN", null)]
    [DataRow("""{"errors":[{"code":1}]}""", null, null)]
    [DataRow("""{"errors":["x"]}""", null, null)]
    [DataRow("""{"errors":[]}""", null, null)]
    [DataRow("""{"errors":{}}""", null, null)]
    [DataRow("""{"other":1}""", null, null)]
    [DataRow("[]", null, null)]
    [DataRow("not json", null, null)]
    public void ReadFirstError_Body_GivesFirstCodeAndMessage(string body, string? code, string? message) =>
        Assert.AreEqual((code, message), OciRegistryError.ReadFirstError(Encoding.UTF8.GetBytes(body)));

    [TestMethod]
    [DataRow(404, """{"errors":[{"code":"NAME_UNKNOWN","message":"no such repo"}]}""", CurlExitCode.HttpReturnedError, "The requested URL returned error: 404 (NAME_UNKNOWN: no such repo)")]
    [DataRow(500, "", CurlExitCode.HttpReturnedError, "The requested URL returned error: 500")]
    [DataRow(400, """{"errors":[{"code":"DENIED","message":"no"}]}""", CurlExitCode.LoginDenied, "The requested URL returned error: 400 (DENIED: no)")]
    [DataRow(400, """{"errors":[{"code":"UNAUTHORIZED","message":"no"}]}""", CurlExitCode.LoginDenied, "The requested URL returned error: 400 (UNAUTHORIZED: no)")]
    [DataRow(401, "", CurlExitCode.LoginDenied, "The requested URL returned error: 401")]
    [DataRow(403, "", CurlExitCode.LoginDenied, "The requested URL returned error: 403")]
    public void From_ErrorResponse_GivesExitCodeAndMessage(int status, string body, CurlExitCode exitCode, string message)
    {
        var failure = OciRegistryError.From(new OciHttpResponse(status, "x", [], Encoding.UTF8.GetBytes(body)));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }
}
