using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>
/// Pins the device authorization grant (RFC 8628): the request (§3.1), what the user is told (§3.3), polling at the
/// interval (§3.4), <c>slow_down</c>, <c>access_denied</c> and <c>expired_token</c> (§3.5), and expiry by
/// <c>expires_in</c> (§3.2), all by a fake <see cref="TimeProvider"/> (ADR-1045).
/// </summary>
[TestClass]
public sealed class OAuth2DeviceAuthorizationFlowTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly Uri DeviceEndpoint = new("https://server.example.com/device_authorization");

    private static readonly OAuth2FlowRequest Request = new(
        new Uri("https://server.example.com/token"), new OAuth2Client("1406020730", null, OAuth2ClientAuthentication.Basic), "read");

    [TestMethod]
    public async Task RunAsync_PendingThenSlowDownThenToken_PollsAtTheIntervalPlusFiveAfterSlowDown()
    {
        var connector = new QueuedConnector(
            new RecordedConnection(Authorization(expiresIn: 1800, interval: 5)),
            new RecordedConnection(FlowResponses.Error("authorization_pending")),
            new RecordedConnection(FlowResponses.Error("slow_down")),
            new RecordedConnection(FlowResponses.Token()));
        var time = new AdvancingTimeProvider(Start);
        var messages = new StringWriter();

        var result = await NewFlow(connector, time, messages).RunAsync(CancellationToken.None);

        Assert.AreEqual("2YotnFZFEjr1zCsicMWpAA", result.Token?.AccessToken);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) }, time.Delays);
        Assert.AreEqual("To sign in, open https://example.com/device and enter the code WDJB-MJHT\n", messages.ToString());
        StringAssert.EndsWith(connector.Connections[0].Written, "\r\n\r\nscope=read&client_id=1406020730");
        StringAssert.EndsWith(
            connector.Connections[3].Written,
            "\r\n\r\ngrant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code&device_code=GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS&client_id=1406020730");
    }

    [TestMethod]
    public async Task RunAsync_AccessDenied_StopsWithLoginDenied()
    {
        var connector = new QueuedConnector(
            new RecordedConnection(Authorization(expiresIn: 1800, interval: 5)),
            new RecordedConnection(FlowResponses.Error("access_denied")));

        var result = await NewFlow(connector, new AdvancingTimeProvider(Start), new StringWriter()).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual(OAuth2ErrorCode.AccessDenied, result.Error?.Code);
    }

    [TestMethod]
    public async Task RunAsync_ExpiredToken_StopsWithOperationTimedOut()
    {
        var connector = new QueuedConnector(
            new RecordedConnection(Authorization(expiresIn: 1800, interval: 5)),
            new RecordedConnection(FlowResponses.Error("expired_token")));

        var result = await NewFlow(connector, new AdvancingTimeProvider(Start), new StringWriter()).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("The token endpoint refused the request: expired_token.", result.Message);
    }

    [TestMethod]
    public async Task RunAsync_ExpiresInPassesWhilePending_StopsWithoutAnotherPoll()
    {
        var connector = new QueuedConnector(
            new RecordedConnection(Authorization(expiresIn: 10, interval: 5)),
            new RecordedConnection(FlowResponses.Error("authorization_pending")));

        var result = await NewFlow(connector, new AdvancingTimeProvider(Start), new StringWriter()).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("The device code expired after 10 seconds before the user signed in.", result.Message);
        Assert.AreEqual(2, connector.ConnectCount);
    }

    [TestMethod]
    public async Task RunAsync_IntervalOfZero_PollsEverySecond()
    {
        var connector = new QueuedConnector(
            new RecordedConnection(Authorization(expiresIn: 1800, interval: 0)),
            new RecordedConnection(FlowResponses.Token()));
        var time = new AdvancingTimeProvider(Start);

        var result = await NewFlow(connector, time, new StringWriter()).RunAsync(CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1) }, time.Delays);
    }

    [TestMethod]
    public async Task RunAsync_DeviceAuthorizationRefused_StopsBeforeTellingTheUser()
    {
        var connector = new QueuedConnector(new RecordedConnection(FlowResponses.Error("unauthorized_client")));
        var messages = new StringWriter();

        var result = await NewFlow(connector, new AdvancingTimeProvider(Start), messages).RunAsync(CancellationToken.None);

        Assert.AreEqual(CurlExitCode.LoginDenied, result.ExitCode);
        Assert.AreEqual("The device authorization endpoint refused the request: unauthorized_client.", result.Message);
        Assert.AreEqual(string.Empty, messages.ToString());
    }

    [TestMethod]
    public void DescribeInstructions_WithACompleteUri_AddsIt()
    {
        var authorization = new OAuth2DeviceAuthorization(
            "GmRh", "WDJB-MJHT", "https://example.com/device", "https://example.com/device?user_code=WDJB-MJHT", 1800, 5);

        Assert.AreEqual(
            "To sign in, open https://example.com/device and enter the code WDJB-MJHT\n" +
            "or open https://example.com/device?user_code=WDJB-MJHT, which carries the code.\n",
            OAuth2DeviceAuthorizationFlow.DescribeInstructions(authorization));
        Assert.ThrowsExactly<ArgumentNullException>(() => OAuth2DeviceAuthorizationFlow.DescribeInstructions(null!));
    }

    private static OAuth2DeviceAuthorizationFlow NewFlow(QueuedConnector connector, TimeProvider time, TextWriter messages) =>
        new(new OAuth2HttpClient(connector), time, messages, Request, DeviceEndpoint);

    // RFC 8628 §3.2's example response.
    private static string Authorization(long expiresIn, long interval) => FlowResponses.Json(
        $$"""{"device_code":"GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS","user_code":"WDJB-MJHT","verification_uri":"https://example.com/device","expires_in":{{expiresIn}},"interval":{{interval}}}""");
}
