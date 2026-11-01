using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Imap;

namespace Curl.Engine;

/// <summary>
/// The handler a <c>CURLOPT_CONNECT_ONLY</c> 1 perform of an <c>imap</c> or <c>imaps</c> URL dispatches to: it runs
/// <see cref="ImapProtocolHandler.ConnectOnlyAsync" /> (IMAP's connect phase: <c>CAPABILITY</c>, <c>STARTTLS</c>, and a
/// login when credentials are given), as libcurl does before a connect-only perform ends, and gives the open connection
/// to <paramref name="connectOnly" /> for <c>curl_easy_recv</c> and <c>curl_easy_send</c> (upstream test660, BL-2623).
/// </summary>
/// <param name="imap">Connects and runs the connect phase.</param>
/// <param name="connectOnly">Keeps the open connection.</param>
internal sealed class ImapConnectOnlyHandler(ImapProtocolHandler imap, ConnectOnlyConnection connectOnly) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => imap.SupportedSchemes;

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        ImapConnectOutcome outcome = await imap.ConnectOnlyAsync(context).ConfigureAwait(false);
        if (outcome.Connection is { } opened)
        {
            await connectOnly.OpenAsync(opened).ConfigureAwait(false);
        }

        return outcome.Result;
    }
}
