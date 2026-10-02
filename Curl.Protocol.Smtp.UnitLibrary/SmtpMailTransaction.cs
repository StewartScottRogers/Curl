using System.Globalization;
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
/// trailing <c>&gt;</c> are taken off it and its host is made an A-label
/// (<see cref="SmtpMailbox" />), so <c>&lt;a@b</c> and <c>a@b</c> both go out as
/// <c>&lt;a@b&gt;</c>; no <c>--mail-from</c> sends <c>MAIL FROM:&lt;&gt;</c>.</item>
/// <item><c>MAIL FROM</c> adds, in this order, <c>AUTH=&lt;addr&gt;</c> for
/// <c>--mail-auth</c> once <c>AUTH</c> succeeded, <c>SIZE=n</c> when <c>EHLO</c> advertised
/// <c>SIZE</c> and the upload can seek and has bytes left, and <c>SMTPUTF8</c> when
/// <c>EHLO</c> advertised it and the reverse path, the <c>AUTH=</c> address or a recipient is
/// not all ASCII (BL-544).</item>
/// <item>Under <c>--mail-rcpt-allowfails</c> a refused <c>RCPT</c> is passed over; only every
/// recipient refused is exit 55, <c>RCPT failed: 551 (last error)</c>, with <c>QUIT</c>.</item>
/// <item><c>MAIL</c> or any <c>RCPT</c> answered other than 2xx, or <c>DATA</c> answered
/// other than 354, is exit 55, <c>MAIL failed: 550</c>; the reply to the end of the message
/// other than 250 is exit 8, <c>Weird server reply</c>. Each still sends <c>QUIT</c>.</item>
/// <item>The server closing before a reply is complete is exit 56, a reply line of 65536
/// bytes exit 100 and a reply line holding a NUL byte exit 8, none with <c>QUIT</c>.</item>
/// <item>Every result carries <see cref="TransferReport.UploadSize" />, the bytes sent after
/// <c>DATA</c> with the stuffed dots and the end-of-data mark, and
/// <see cref="TransferReport.ResponseCode" />, the last reply's code, which is 0 once the
/// message is sent until its reply arrives. Progress reports the same count against the
/// upload's length, or against nothing when the upload cannot seek.</item>
/// </list>
/// </remarks>
internal sealed class SmtpMailTransaction(
    SmtpControlChannel channel, ITransferContext context, SmtpMailExtensions extensions, SmtpCommandLineText commandLineText)
{
    private const int ReadBufferSize = 65536;

    private const int DataAccepted = 354;

    private const int MessageAccepted = 250;

    /// <summary>The state curl's SMTP state machine waits for the reply to the message in.</summary>
    private const string PostData = "POSTDATA";

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
            long? size = RemainingLength(upload);
            result = await SendEnvelopeAsync(mail, size).ConfigureAwait(false)
                ?? await SendMessageAsync(upload, size).ConfigureAwait(false);
            await channel.QuitAsync().ConfigureAwait(false);
        }
        catch (SmtpReplyMissingException)
        {
            result = TransferResult.Failure(CurlExitCode.RecvError, SmtpSessionMessages.ResponseReadingFailed);
        }
        catch (SmtpNulByteInReplyException)
        {
            result = TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.NulByteInResponseLine);
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

    private static TransferResult CommandFailed(string command, SmtpReply reply) =>
        TransferResult.Failure(CurlExitCode.SendError, SmtpSessionMessages.CommandFailed(command, reply.Code));

    /// <summary>The bytes left in <paramref name="upload" />, or <see langword="null" /> when it cannot seek.</summary>
    private static long? RemainingLength(Stream upload) => upload.CanSeek ? Math.Max(0, upload.Length - upload.Position) : null;

    /// <summary>
    /// Writes <c>MAIL FROM</c> with the parameters curl adds, in curl's order: <c>AUTH=</c>
    /// once authenticated, <c>SIZE=</c> when advertised and the size is known and not 0, and
    /// <c>SMTPUTF8</c> when advertised and the reverse path, the <c>AUTH=</c> address or a
    /// recipient is not all ASCII.
    /// </summary>
    private string MailCommand(MailRequestOptions mail, long? size)
    {
        string? auth = extensions.Authenticated ? mail.Auth : null;
        return "MAIL FROM:" + SmtpMailbox.Bracketed(mail.From, commandLineText)
            + (auth is null ? string.Empty : " AUTH=" + SmtpMailbox.Bracketed(auth, commandLineText))
            + SizeParameter(size)
            + (extensions.SmtpUtf8Advertised && NeedsSmtpUtf8(mail, auth) ? " SMTPUTF8" : string.Empty);
    }

    private bool NeedsSmtpUtf8(MailRequestOptions mail, string? auth) =>
        SmtpMailbox.NeedsSmtpUtf8(mail.From, commandLineText) || SmtpMailbox.NeedsSmtpUtf8(auth, commandLineText)
        || mail.Recipients.Any(recipient => SmtpMailbox.NeedsSmtpUtf8(recipient, commandLineText));

    private string SizeParameter(long? size) =>
        extensions.SizeAdvertised && size is > 0 and long known
            ? " SIZE=" + known.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

    private async ValueTask<TransferResult?> SendEnvelopeAsync(MailRequestOptions mail, long? size)
    {
        SmtpReply reply = await ExchangeAsync(MailCommand(mail, size), "MAIL").ConfigureAwait(false);
        if (!reply.IsCompletion)
        {
            return CommandFailed("MAIL", reply);
        }

        if (await SendRecipientsAsync(mail).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        reply = await ExchangeAsync("DATA", "DATA").ConfigureAwait(false);
        if (reply.Code != DataAccepted)
        {
            return CommandFailed("DATA", reply);
        }

        channel.Trace.DoingDone();
        return null;
    }

    /// <summary>
    /// Sends <c>RCPT TO</c> for each recipient. The first refusal stops the message, unless
    /// <c>--mail-rcpt-allowfails</c> asks to carry on, when only every recipient refused does,
    /// with the last refusal's code.
    /// </summary>
    private async ValueTask<TransferResult?> SendRecipientsAsync(MailRequestOptions mail)
    {
        bool anyAccepted = false;
        int lastRefusal = 0;
        foreach (string recipient in mail.Recipients)
        {
            SmtpReply reply = await ExchangeAsync("RCPT TO:" + SmtpMailbox.Bracketed(recipient, commandLineText), "RCPT").ConfigureAwait(false);
            if (reply.IsCompletion)
            {
                anyAccepted = true;
            }
            else if (mail.RecipientAllowFails)
            {
                lastRefusal = reply.Code;
            }
            else
            {
                return CommandFailed("RCPT", reply);
            }
        }

        return anyAccepted
            ? null
            : TransferResult.Failure(CurlExitCode.SendError, SmtpSessionMessages.EveryRecipientRefused(lastRefusal));
    }

    private async ValueTask<TransferResult> SendMessageAsync(Stream upload, long? expected)
    {
        // The last piece goes out with the end-of-data mark, one send and one data event, as
        // curl sends a message that fits its buffer (measured, BL-546). Under --trace-config smtp
        // each piece goes out as it is read and the mark on its own, as curl then sends them (BL-1163),
        // and so does an upload of unknown size such as standard input, whose end curl learns only
        // from a read that returns nothing (BL-1198).
        SmtpStateTrace trace = channel.Trace;
        bool sendsEachRead = trace.Enabled || expected is null;
        var stuffer = new SmtpDotStuffer();
        byte[] buffer = new byte[ReadBufferSize];
        byte[] pending = [];
        int read;
        while ((read = await upload.ReadAsync(buffer, context.CancellationToken).ConfigureAwait(false)) > 0)
        {
            trace.BodyRead(read);
            await SendMessageBytesAsync(pending, expected).ConfigureAwait(false);
            pending = stuffer.Encode(buffer.AsSpan(0, read));
            if (sendsEachRead)
            {
                await SendMessageBytesAsync(pending, expected).ConfigureAwait(false);
                pending = [];
            }
        }

        trace.BodyEnded();
        await SendMessageBytesAsync([.. pending, .. stuffer.EndOfData], expected).ConfigureAwait(false);
        context.Events.ReportInfo(SmtpConnectionInfoLines.UploadSent(uploaded));
        trace.Enter(PostData);

        // Measured: curl reports response code 000 when the server closes instead of answering the message.
        responseCode = 0;
        SmtpReply reply = await ReadReplyAsync().ConfigureAwait(false);
        if (reply.Code != MessageAccepted)
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.WeirdServerReply);
        }

        trace.Enter("STOP");
        return TransferResult.Success(0);
    }

    /// <summary>Sends <paramref name="bytes" /> after <c>DATA</c>; nothing at all when there are none.</summary>
    private async ValueTask SendMessageBytesAsync(byte[] bytes, long? expected)
    {
        if (bytes.Length == 0)
        {
            return;
        }

        await channel.SendBytesAsync(bytes).ConfigureAwait(false);
        uploaded += bytes.Length;
        context.Progress.ReportUploaded(uploaded, expected);
    }

    /// <summary>Sends <paramref name="command" />, which puts curl's state machine in <paramref name="state" />, and reads its reply.</summary>
    private async ValueTask<SmtpReply> ExchangeAsync(string command, string state)
    {
        await channel.SendAsync(command).ConfigureAwait(false);
        channel.Trace.CommandSent(state);
        return await ReadReplyAsync().ConfigureAwait(false);
    }

    private async ValueTask<SmtpReply> ReadReplyAsync()
    {
        SmtpReply reply = await channel.ReadReplyAsync().ConfigureAwait(false) ?? throw new SmtpReplyMissingException();
        responseCode = reply.Code;
        return reply;
    }
}
