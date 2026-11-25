using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// STD-167: a rejected HTTP/0.9 reply exits 1 but the handle had already connected over http, so <c>%{scheme}</c> prints
/// <c>http</c> as curl does, unlike an unsupported scheme.
/// </summary>
[TestClass]
public sealed class Std167Tests
{
    [TestMethod]
    public void RequestSchemeOf_Http09Rejected_IsTheRequestUrlsScheme()
    {
        TransferResult result = TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Received HTTP/0.9 when not allowed");

        Assert.AreEqual("http", TransferInfoFromReport.RequestSchemeOf("http://127.0.0.1:23001/nostatus", result));
    }
}
