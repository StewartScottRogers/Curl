using System.Net;
using Curl.Authentication;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <content>
/// Pins curl's error message for <c>--negotiate -u : -f</c> against a <c>401 Negotiate</c> with
/// no ticket: the context's failure line, not <c>The requested URL returned error: 401</c>, as
/// curl 8.21.0's first <c>failf</c> fills its error buffer (measured, BL-955 Notes; ADR-0344).
/// </content>
public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketFailOnWindows_FailsWithSspisFailureLine()
    {
        TransferResult result = await NegotiateWithoutATicketResultAsync(NegotiateDenied, HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(SspiNoCredentials[2..], result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketFailOffWindows_FailsWithGssApisFailureLine()
    {
        TransferResult result = await NegotiateWithoutATicketResultAsync(NegotiateDenied, HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(GssApiNoCredentials[2..], result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketEmptyReply_FailsWithTheFailureLine()
    {
        TransferResult result = await NegotiateWithoutATicketResultAsync(string.Empty, HttpFailMode.None);

        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(SspiNoCredentials[2..], result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailWithoutNegotiate_KeepsTheReturnedErrorMessage()
    {
        TurnTakingConnection connection = new(65536, NegotiateDenied);
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Http = new HttpRequestOptions { Fail = HttpFailMode.Fail },
        };

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), new ScriptedTokenSource()).ExecuteAsync(context);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual("The requested URL returned error: 401", result.ErrorMessage);
    }

    /// <summary>
    /// Runs <c>--negotiate -u :</c> with <paramref name="fail" /> against
    /// <paramref name="response" />, every context step failing for want of a ticket.
    /// </summary>
    private static async Task<TransferResult> NegotiateWithoutATicketResultAsync(string response, HttpFailMode fail)
    {
        TurnTakingConnection connection = new(65536, response);
        ScriptedTokenSource tokens = new(
            new SecurityContextStep(SecurityContextStatus.NoCredentials, []),
            new SecurityContextStep(SecurityContextStatus.NoCredentials, []));
        TransferContext context = new()
        {
            Url = CurlUrl.Parse(AuthUrl),
            Output = new MemoryStream(),
            Credentials = new NetworkCredential(string.Empty, string.Empty),
            Http = new HttpRequestOptions { AuthSchemes = HttpAuthSchemes.Negotiate, Fail = fail },
        };

        return await NegotiateHandler(QueueConnector.For(connection), tokens).ExecuteAsync(context);
    }
}
