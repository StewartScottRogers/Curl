using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp;

namespace Curl.Engine;

/// <summary>
/// The handler a <c>CURLOPT_CONNECT_ONLY</c> 1 perform of an <c>ftp</c> or <c>ftps</c> URL dispatches to: it runs
/// <see cref="FtpProtocolHandler.ConnectOnlyAsync" /> (FTP's connect phase: <c>USER</c>, <c>PASS</c>, <c>PWD</c>), as
/// libcurl logs in before a connect-only perform ends, and gives the open control connection to
/// <paramref name="connectOnly" /> for <c>curl_easy_recv</c> and <c>curl_easy_send</c> (upstream test597, BL-2623).
/// </summary>
/// <param name="ftp">Connects and logs in.</param>
/// <param name="connectOnly">Keeps the logged-in connection.</param>
internal sealed class FtpConnectOnlyHandler(FtpProtocolHandler ftp, ConnectOnlyConnection connectOnly) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => ftp.SupportedSchemes;

    /// <inheritdoc />
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        FtpConnectOnlyOutcome outcome = await ftp.ConnectOnlyAsync(context).ConfigureAwait(false);
        if (outcome.Connection is { } opened)
        {
            await connectOnly.OpenAsync(opened).ConfigureAwait(false);
        }

        return outcome.Result;
    }
}
