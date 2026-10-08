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
        Diagnostics.Arrange("scripted response, fail mode", "401 Negotiate, Fail");
        TransferResult result = await NegotiateWithoutATicketResultAsync(NegotiateDenied, HttpFailMode.Fail);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Diagnostics.Assert("error message", SspiNoCredentials[2..], result.ErrorMessage);
        Assert.AreEqual(SspiNoCredentials[2..], result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketFailOffWindows_FailsWithGssApisFailureLine()
    {
        Diagnostics.Arrange("scripted response, fail mode", "401 Negotiate, Fail");
        TransferResult result = await NegotiateWithoutATicketResultAsync(NegotiateDenied, HttpFailMode.Fail);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Diagnostics.Assert("error message", GssApiNoCredentials[2..], result.ErrorMessage);
        Assert.AreEqual(GssApiNoCredentials[2..], result.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_NegotiateWithoutATicketEmptyReply_FailsWithTheFailureLine()
    {
        Diagnostics.Arrange("scripted response, fail mode", "(empty), None");
        TransferResult result = await NegotiateWithoutATicketResultAsync(string.Empty, HttpFailMode.None);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.GotNothing, result.ExitCode);
        Assert.AreEqual(CurlExitCode.GotNothing, result.ExitCode);
        Diagnostics.Assert("error message", SspiNoCredentials[2..], result.ErrorMessage);
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

        Diagnostics.Arrange("scripted response, fail mode", "401 Negotiate, Fail, no negotiate scheme");

        TransferResult result = await NegotiateHandler(QueueConnector.For(connection), new ScriptedTokenSource()).ExecuteAsync(context);

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.HttpReturnedError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode);
        Diagnostics.Assert("error message", "The requested URL returned error: 401", result.ErrorMessage);
        Assert.AreEqual("The requested URL returned error: 401", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(CurlExitCode.CouldntConnect, "Failed to connect to 127.0.0.1 port 50998 after 0 ms: Could not connect to server")]
    [DataRow(CurlExitCode.InterfaceFailed, "Failed to connect to 127.0.0.1 port 50998 after 0 ms: Failed binding local connection end")]
    public async Task ExecuteAsync_NegotiateWithoutATicketConnectFails_KeepsTheConnectFailure(CurlExitCode exitCode, string connectFailure)
    {
        Diagnostics.Arrange("connect failure", connectFailure);
        TransferResult result = await NegotiateWithoutATicketResultAsync(new QueueConnector(ConnectResult.Failed(exitCode, connectFailure)), HttpFailMode.None);

        WriteResult(result);
        Diagnostics.Assert("exit code", exitCode, result.ExitCode);
        Assert.AreEqual(exitCode, result.ExitCode);
        Diagnostics.Assert("error message", connectFailure, result.ErrorMessage);
        Assert.AreEqual(connectFailure, result.ErrorMessage);
    }

    /// <summary>
    /// Runs <c>--negotiate -u :</c> with <paramref name="fail" /> against
    /// <paramref name="response" />, every context step failing for want of a ticket.
    /// </summary>
    private static Task<TransferResult> NegotiateWithoutATicketResultAsync(string response, HttpFailMode fail) =>
        NegotiateWithoutATicketResultAsync(QueueConnector.For(new TurnTakingConnection(65536, response)), fail);

    /// <summary>
    /// Runs <c>--negotiate -u :</c> with <paramref name="fail" /> through
    /// <paramref name="connector" />, every context step failing for want of a ticket.
    /// </summary>
    private static async Task<TransferResult> NegotiateWithoutATicketResultAsync(QueueConnector connector, HttpFailMode fail)
    {
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

        return await NegotiateHandler(connector, tokens).ExecuteAsync(context);
    }
}
