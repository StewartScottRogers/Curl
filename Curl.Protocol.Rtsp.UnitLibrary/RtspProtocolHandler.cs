using System.Globalization;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Serves the <c>rtsp</c> scheme as the curl 8.21.0 tool does: connects, sends one
/// <c>OPTIONS * RTSP/1.0</c> request with <c>CSeq: 1</c>, writes the reply head to the header
/// output, reads and discards the body, and fails a reply whose <c>CSeq</c> does not match with
/// exit 85 (ADR-0169).
/// </summary>
/// <param name="connector">
/// Supplies the connection to the URL's host and port (554 when the URL names none),
/// tunnelled through <see cref="ITransferContext.Proxy" /> when one is set. No
/// <see cref="System.Net.Sockets.Socket" /> is ever constructed here.
/// </param>
/// <param name="authenticator">
/// Builds the pre-emptive <c>Authorization</c> value for <c>-u</c>, <c>--basic</c> and
/// <c>--oauth2-bearer</c>, with no challenges.
/// </param>
/// <remarks>
/// Measured against a loopback listener (ADR-0169, BL-591). <c>-X</c>, <c>--request-target</c>,
/// <c>-d</c>, <c>-b</c> and <c>--compressed</c> change nothing; the reply body never reaches
/// the output, even with <c>-i</c>. A <c>-H</c> header naming <c>CSeq</c> fails with 85,
/// <c>CSeq cannot be set as a custom header.</c>, after connecting and before anything is sent.
/// Once the head has ended: under <c>-f</c> a status of 400 or more fails with 22,
/// <c>The requested URL returned error: &lt;code&gt;</c>, ahead of any <c>CSeq</c> check; a
/// status below 100 fails with 1, <c>Unsupported response code in HTTP response</c>. A reply
/// <c>CSeq</c> other than the one sent, or none, fails with 85,
/// <c>The CSeq of this request 1 did not match the response &lt;received&gt;</c>, also when the
/// server closed before the head ended. A connect failure is returned as the connector
/// reported it.
/// </remarks>
public sealed class RtspProtocolHandler(IConnector connector, IHttpAuthenticator authenticator) : IProtocolHandler
{
    /// <summary>The port an <c>rtsp</c> URL without one connects to.</summary>
    internal const int DefaultPort = 554;

    /// <summary>The <c>CSeq</c> of a transfer's first request.</summary>
    internal const long FirstSequenceNumber = 1;

    /// <summary>The request target of an <c>OPTIONS</c> request.</summary>
    private const string OptionsTarget = "*";

    private const int LowestStatusCode = 100;

    private const int LowestErrorStatusCode = 400;

    private const string UnsupportedResponseCode = "Unsupported response code in HTTP response";

    /// <summary>
    /// The one scheme this handler serves, as curl 8.21.0's <c>--version</c> protocol list
    /// names it.
    /// </summary>
    private static readonly string[] Schemes = ["rtsp"];

    private readonly IConnector connector =
        connector ?? throw new ArgumentNullException(nameof(connector));

    private readonly IHttpAuthenticator authenticator =
        authenticator ?? throw new ArgumentNullException(nameof(authenticator));

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => Schemes;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <see cref="ITransferContext.CancellationToken" /> was cancelled.
    /// </exception>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        CurlUrl url = context.Url;
        var target = new ConnectTarget(url.IdnHost, url.IsDefaultPort ? DefaultPort : url.Port, false)
        {
            Proxy = context.Proxy,
            Events = context.Events,
        };
        ConnectResult connect = await connector.ConnectAsync(target, context.CancellationToken).ConfigureAwait(false);
        if (connect.Connection is not { } connection)
        {
            return new TransferResult(connect.ExitCode, 0, connect.ErrorMessage) { IsConnectionRefused = connect.IsConnectionRefused };
        }

        context.Progress.ReportTransferStarted();
        await using (connection.ConfigureAwait(false))
        {
            try
            {
                return await ExchangeAsync(connection, context).ConfigureAwait(false);
            }
            catch (RtspTransferException failure)
            {
                return TransferResult.Failure(failure.ExitCode, failure.Message);
            }
        }
    }

    private static string SequenceMismatch(long sent, long received) =>
        string.Create(CultureInfo.InvariantCulture, $"The CSeq of this request {sent} did not match the response {received}");

    private static async ValueTask SendAsync(IConnection connection, byte[] request, CancellationToken cancellationToken)
    {
        try
        {
            await connection.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw RtspIoFailures.SendFailed(exception);
        }
    }

    /// <summary>
    /// Sends the <c>OPTIONS</c> request and reads its reply, checking the status and the
    /// <c>CSeq</c> in curl's order.
    /// </summary>
    private async Task<TransferResult> ExchangeAsync(IConnection connection, ITransferContext context)
    {
        HttpRequestOptions options = context.Http ?? new HttpRequestOptions();
        if (RtspRequestFormatter.NamesCSeq(options))
        {
            return TransferResult.Failure(CurlExitCode.RtspCseqError, RtspRequestFormatter.CustomCSeqRefused);
        }

        byte[] request = RtspRequestFormatter.Format(
            RtspMethod.Options,
            OptionsTarget,
            FirstSequenceNumber,
            sessionId: null,
            options,
            Authorization(context, options));
        await SendAsync(connection, request, context.CancellationToken).ConfigureAwait(false);
        RtspReplyHead head = await RtspReplyReader.ReadHeadAsync(connection, context.HeaderOutput, context.CancellationToken).ConfigureAwait(false);
        TransferReport report = new()
        {
            ResponseCode = head.StatusCode,
            Method = RtspMethod.Options.Name,
            HeaderSize = head.Length,
            RequestSize = request.Length,
        };
        if (StatusFailure(head, options) is { } statusFailure)
        {
            return statusFailure with { Report = report };
        }

        long bodyRead = await RtspReplyReader.DiscardBodyAsync(connection, head.Remaining, head.ContentLength, context.Progress, context.CancellationToken).ConfigureAwait(false);
        context.Progress.ReportTransferDone();
        TransferResult result = head.SequenceNumber == FirstSequenceNumber
            ? TransferResult.Success(bodyRead)
            : TransferResult.Failure(CurlExitCode.RtspCseqError, SequenceMismatch(FirstSequenceNumber, head.SequenceNumber), bodyRead);
        return result with { Report = report with { DownloadSize = bodyRead } };
    }

    /// <summary>
    /// Fails a completed head whose status curl refuses: 400 or more under <c>-f</c> with 22,
    /// below 100 with 1.
    /// </summary>
    /// <returns>The failure, or <see langword="null" /> when the status is accepted.</returns>
    private static TransferResult? StatusFailure(RtspReplyHead head, HttpRequestOptions options)
    {
        if (!head.IsComplete)
        {
            return null;
        }

        if (options.Fail != HttpFailMode.None && head.StatusCode >= LowestErrorStatusCode)
        {
            return TransferResult.Failure(
                CurlExitCode.HttpReturnedError,
                string.Create(CultureInfo.InvariantCulture, $"The requested URL returned error: {head.StatusCode}"));
        }

        return head.StatusCode < LowestStatusCode
            ? TransferResult.Failure(CurlExitCode.UnsupportedProtocol, UnsupportedResponseCode)
            : null;
    }

    private string? Authorization(ITransferContext context, HttpRequestOptions options) =>
        authenticator.CreateAuthorization(
            new HttpAuthRequest(
                RtspMethod.Options.Name,
                context.Url,
                OptionsTarget,
                context.Credentials,
                options.BearerToken,
                options.AuthSchemes,
                IsProxy: false),
            []);
}
