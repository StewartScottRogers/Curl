using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Sends one message on an open SMTP session: <c>MAIL FROM</c>, one <c>RCPT TO</c> per
/// <c>--mail-rcpt</c>, <c>DATA</c>, the dot-stuffed upload (<see cref="SmtpDotStuffer" />) and
/// <c>QUIT</c>, each step and each failure's exit code measured on curl 8.21.0 with
/// <c>Record-CurlExchange.ps1 -Smtp</c> (BL-542).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>An address is sent inside angle brackets after one leading <c>&lt;</c> and one
/// trailing <c>&gt;</c> are taken off it, so <c>&lt;a@b</c> and <c>a@b</c> both go out as
/// <c>&lt;a@b&gt;</c>; no <c>--mail-from</c> sends <c>MAIL FROM:&lt;&gt;</c>.</item>
/// <item><c>MAIL</c> or any <c>RCPT</c> answered other than 2xx, or <c>DATA</c> answered
/// other than 354, is exit 55, <c>MAIL failed: 550</c>; the reply to the end of the message
/// other than 250 is exit 8, <c>Weird server reply</c>. Each still sends <c>QUIT</c>.</item>
/// <item>The server closing before a reply is complete is exit 56 and a reply line of 65536
/// bytes exit 100, neither with <c>QUIT</c>.</item>
/// <item>Every result carries <see cref="TransferReport.UploadSize" />, the bytes sent after
/// <c>DATA</c> with the stuffed dots and the end-of-data mark, and
/// <see cref="TransferReport.ResponseCode" />, the last reply's code, which is 0 once the
/// message is sent until its reply arrives. Progress reports the same count against the
/// upload's length, or against nothing when the upload cannot seek.</item>
/// </list>
/// </remarks>
internal sealed class SmtpMailTransaction(SmtpControlChannel channel, ITransferContext context)
{
    private const int ReadBufferSize = 65536;

    private const int DataAccepted = 354;

    private const int MessageAccepted = 250;

    private int responseCode = channel.LastReplyCode;

    private long uploaded;

    /// <summary>
    /// Sends the message read from <paramref name="upload" /> to every recipient in
    /// <paramref name="mail" /> and closes the session.
    /// </summary>
    /// <param name="upload">The message, sent from its current position to its end.</param>
    /// <param name="mail">The reverse path and the recipients.</param>
    /// <returns>A success, or the failure that stopped the message, with the report on it.</returns>
    public async ValueTask<TransferResult> SendAsync(Stream upload, MailRequestOptions mail)
    {
        TransferResult result;
        try
        {
            result = await SendEnvelopeAsync(mail).ConfigureAwait(false)
                ?? await SendMessageAsync(upload).ConfigureAwait(false);
            await channel.QuitAsync().ConfigureAwait(false);
        }
        catch (SmtpReplyMissingException)
        {
            result = TransferResult.Failure(CurlExitCode.RecvError, SmtpSessionMessages.ResponseReadingFailed);
        }
        catch (InvalidDataException)
        {
            result = TransferResult.Failure(CurlExitCode.TooLarge, SmtpSessionMessages.ReplyLineTooLarge);
        }

        return result with
        {
            BytesTransferred = uploaded,
            Report = new TransferReport { ResponseCode = responseCode, UploadSize = uploaded },
        };
    }

    private static string Bracket(string? address)
    {
        string bare = address ?? string.Empty;
        bare = bare.StartsWith('<') ? bare[1..] : bare;
        bare = bare.EndsWith('>') ? bare[..^1] : bare;
        return "<" + bare + ">";
    }

    private static TransferResult CommandFailed(string command, SmtpReply reply) =>
        TransferResult.Failure(CurlExitCode.SendError, SmtpSessionMessages.CommandFailed(command, reply.Code));

    private async ValueTask<TransferResult?> SendEnvelopeAsync(MailRequestOptions mail)
    {
        SmtpReply reply = await ExchangeAsync("MAIL FROM:" + Bracket(mail.From)).ConfigureAwait(false);
        if (!reply.IsCompletion)
        {
            return CommandFailed("MAIL", reply);
        }

        foreach (string recipient in mail.Recipients)
        {
            reply = await ExchangeAsync("RCPT TO:" + Bracket(recipient)).ConfigureAwait(false);
            if (!reply.IsCompletion)
            {
                return CommandFailed("RCPT", reply);
            }
        }

        reply = await ExchangeAsync("DATA").ConfigureAwait(false);
        return reply.Code == DataAccepted ? null : CommandFailed("DATA", reply);
    }

    private async ValueTask<TransferResult> SendMessageAsync(Stream upload)
    {
        long? expected = upload.CanSeek ? Math.Max(0, upload.Length - upload.Position) : null;
        var stuffer = new SmtpDotStuffer();
        byte[] buffer = new byte[ReadBufferSize];
        int read;
        while ((read = await upload.ReadAsync(buffer, context.CancellationToken).ConfigureAwait(false)) > 0)
        {
            await SendMessageBytesAsync(stuffer.Encode(buffer.AsSpan(0, read)), expected).ConfigureAwait(false);
        }

        await SendMessageBytesAsync(stuffer.EndOfData, expected).ConfigureAwait(false);

        // Measured: curl reports response code 000 when the server closes instead of answering the message.
        responseCode = 0;
        SmtpReply reply = await ReadReplyAsync().ConfigureAwait(false);
        return reply.Code == MessageAccepted
            ? TransferResult.Success(0)
            : TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.WeirdServerReply);
    }

    private async ValueTask SendMessageBytesAsync(byte[] bytes, long? expected)
    {
        await channel.SendBytesAsync(bytes).ConfigureAwait(false);
        uploaded += bytes.Length;
        context.Progress.ReportUploaded(uploaded, expected);
    }

    private async ValueTask<SmtpReply> ExchangeAsync(string command)
    {
        await channel.SendAsync(command).ConfigureAwait(false);
        return await ReadReplyAsync().ConfigureAwait(false);
    }

    private async ValueTask<SmtpReply> ReadReplyAsync()
    {
        SmtpReply reply = await channel.ReadReplyAsync().ConfigureAwait(false) ?? throw new SmtpReplyMissingException();
        responseCode = reply.Code;
        return reply;
    }
}
