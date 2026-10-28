using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-194: <c>%{scheme}</c> is the scheme of the last URL a transfer reached, as curl 8.22.0 prints it: a followed
/// redirect to <c>ftp</c> that fails to connect prints <c>ftp</c>, and a redirect <c>--proto-redir</c> refuses prints
/// the scheme of the URL it came from.
/// </summary>
[TestClass]
public sealed class Cgr194Tests
{
    [TestMethod]
    public void RequestSchemeOf_RedirectFollowedToFtpThenFailed_IsFtp()
    {
        TransferResult result = TransferResult.Failure(CurlExitCode.CouldntConnect, "Could not connect to server") with
        {
            Report = new TransferReport { EffectiveUrl = "ftp://127.0.0.1:20299/y" },
        };

        Assert.AreEqual("ftp", TransferInfoFromReport.RequestSchemeOf("http://127.0.0.1:20200/x", result));
    }

    [TestMethod]
    public void RequestSchemeOf_RedirectRefusedByProtoRedir_IsTheRequestUrlsScheme()
    {
        TransferResult result = TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Protocol \"http\" is disabled (in redirect)") with
        {
            Report = new TransferReport { EffectiveUrl = "http://127.0.0.1:20200/x" },
        };

        Assert.AreEqual("http", TransferInfoFromReport.RequestSchemeOf("http://127.0.0.1:20200/x", result));
    }

    [TestMethod]
    public void RequestSchemeOf_UnsupportedSchemeOutsideARedirect_IsNull()
    {
        TransferResult result = TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Unsupported protocol");

        Assert.IsNull(TransferInfoFromReport.RequestSchemeOf("xyz://a/b", result));
    }

    [TestMethod]
    public void RequestSchemeOf_NoReport_IsTheRequestUrlsScheme()
    {
        Assert.AreEqual("http", TransferInfoFromReport.RequestSchemeOf("http://h/", TransferResult.Failure(CurlExitCode.CouldntConnect, "x")));
    }
}
