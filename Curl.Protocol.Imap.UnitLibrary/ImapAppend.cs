using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// Uploads the <c>-T</c> message to a mailbox with <c>APPEND</c>, as curl 8.21.0's
/// <c>imap_perform_append</c> does and as measured with <c>Record-CurlExchange.ps1 -Imap</c>
/// (BL-557).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>No mailbox in the URL is exit 3 <c>Cannot APPEND without a mailbox.</c>, and an
/// upload whose size cannot be known first (it cannot seek, as with <c>-T -</c>) exit 25
/// <c>Cannot APPEND with unknown input file size</c>; neither sends <c>APPEND</c>.</item>
/// <item>Otherwise <c>APPEND mailbox (\Flag ...) {n}</c> is sent, the mailbox as
/// <see cref="ImapQuoting" /> writes it, then each <c>--upload-flags</c> name with a
/// <c>\</c> and its first letter in capitals, in the order given (the command line gives
/// curl's), and no parentheses at all when there is none. A <c>UID</c> or other URL
/// parameter changes nothing.</item>
/// <item>A <c>+</c> continuation is answered with the message's n bytes and a CRLF; any other
/// answer is exit 25 with curl's error text and nothing uploaded. The completion then read
/// is a success when <c>OK</c> and exit 25 otherwise, the bytes sent reported either way as
/// <c>%{size_upload}</c>.</item>
/// </list>
/// </remarks>
internal sealed class ImapAppend(ImapControlChannel channel, ITransferContext context)
{
    private const int ReadBufferSize = 64 * 1024;

    private static readonly Func<string, bool> NoUntagged = static _ => false;

    private long uploaded;

    /// <summary>
    /// Appends <paramref name="upload" /> to <paramref name="mailbox" />. The caller sends
    /// <c>LOGOUT</c> afterwards, whatever this returns.
    /// </summary>
    /// <param name="mailbox">The URL's decoded mailbox, or <see langword="null" /> when it names none.</param>
    /// <param name="upload">The message to upload, from its current position.</param>
    /// <returns>A success, or the failure that stopped the upload.</returns>
    /// <exception cref="ImapResponseMissingException">The server closed before a response was complete.</exception>
    public async ValueTask<TransferResult> AppendAsync(string? mailbox, Stream upload)
    {
        if (mailbox is null)
        {
            return TransferResult.Failure(CurlExitCode.UrlMalformat, ImapSessionMessages.AppendWithoutMailbox);
        }

        if (!upload.CanSeek)
        {
            return TransferResult.Failure(CurlExitCode.UploadFailed, ImapSessionMessages.AppendWithUnknownSize);
        }

        long size = Math.Max(0, upload.Length - upload.Position);
        await channel.SendCommandAsync(AppendCommandOf(mailbox, size)).ConfigureAwait(false);
        ImapResponse answer = await ReadResponseAsync(acceptsContinuation: true).ConfigureAwait(false);
        if (answer.Status != ImapResponseStatus.Continuation)
        {
            return TransferResult.Failure(CurlExitCode.UploadFailed, ImapSessionMessages.UploadFailed);
        }

        await SendMessageAsync(upload, size).ConfigureAwait(false);
        ImapResponse completion = await ReadResponseAsync(acceptsContinuation: false).ConfigureAwait(false);
        TransferResult result = completion.Status == ImapResponseStatus.Ok
            ? TransferResult.Success(uploaded)
            : TransferResult.Failure(CurlExitCode.UploadFailed, ImapSessionMessages.UploadFailed, uploaded);
        return result with { Report = new TransferReport { UploadSize = uploaded } };
    }

    /// <summary>
    /// The <c>APPEND</c> command for <paramref name="size" /> bytes to <paramref name="mailbox" />,
    /// with the <c>--upload-flags</c> flags in parentheses when there are any.
    /// </summary>
    private string AppendCommandOf(string mailbox, long size)
    {
        IReadOnlyList<string> flags = context.Mail?.UploadFlags ?? [];
        string flagList = flags.Count == 0
            ? string.Empty
            : " (" + string.Join(' ', flags.Select(static name => "\\" + name[..1].ToUpperInvariant() + name[1..])) + ")";
        return string.Create(CultureInfo.InvariantCulture, $"APPEND {ImapQuoting.AtomOrQuoted(mailbox)}{flagList} {{{size}}}");
    }

    /// <summary>
    /// Sends the message's bytes, reporting each piece to the progress meter against
    /// <paramref name="size" />, then the CRLF that ends the <c>APPEND</c> command.
    /// </summary>
    private async ValueTask SendMessageAsync(Stream upload, long size)
    {
        context.Progress.ReportTransferStarted();
        byte[] buffer = new byte[ReadBufferSize];
        int read;
        while ((read = await upload.ReadAsync(buffer, context.CancellationToken).ConfigureAwait(false)) > 0)
        {
            await channel.SendBytesAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
            uploaded += read;
            context.Progress.ReportUploaded(uploaded, size);
        }

        await channel.SendLineAsync(string.Empty).ConfigureAwait(false);
    }

    private async ValueTask<ImapResponse> ReadResponseAsync(bool acceptsContinuation) =>
        await channel.ReadResponseAsync(NoUntagged, acceptsContinuation).ConfigureAwait(false)
            ?? throw new ImapResponseMissingException();
}
