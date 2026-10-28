using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins CGR-216: real curl 8.22.0 measured here gives <c>%{num_headers}</c> after <c>-L</c> through
/// a body-less redirect as the last response's headers only, so earlier hops are not added.
/// </summary>
[TestClass]
public sealed class Cgr216Tests
{
    [TestMethod]
    public void TryGetVariableText_NumHeadersAfterBodylessRedirect_CountsOnlyTheLastResponse()
    {
        TransferReport report = new()
        {
            EarlierResponseHeaders = [new("Location", "/x"), new("Content-Length", "0"), new("Connection", "close")],
            ResponseHeaders = [new("Date", "d"), new("Content-Type", "text/plain"), new("Content-Length", "2"), new("Connection", "close")],
        };
        TransferWriteOutVariables variables = new(
            TransferResult.Success(0) with { Report = report }, "http://h/", 0, "http://h/", "http", TimeProvider.System);

        bool found = variables.TryGetVariableText("num_headers", out string? text);

        Assert.IsTrue(found);
        Assert.AreEqual("4", text);
    }
}
