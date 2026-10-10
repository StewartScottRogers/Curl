using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Sends the commands of an SMTP session that has no message to send: <c>VRFY</c> for each
/// <c>--mail-rcpt</c>, the <c>-X</c> command for each <c>--mail-rcpt</c>, or, with no
/// recipient, the <c>-X</c> command alone or <c>HELP</c>, writing each reply to the output,
/// then <c>QUIT</c>. Each step and each failure's exit code was measured on curl 8.21.0 with
/// <c>Record-CurlExchange.ps1 -Smtp</c> (BL-543, ADR-0135).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>VRFY</c> sends the address bare (<see cref="SmtpMailbox.Bare" />: its brackets,
/// and anything after the last <c>&gt;</c> of one starting <c>&lt;</c>, taken off), its host
/// part converted to an IDNA A-label, and
/// <c> SMTPUTF8</c> appended when the <c>EHLO</c> reply advertised <c>SMTPUTF8</c> and the
/// address is not all ASCII, all in the argv bytes of <see cref="SmtpCommandLineText" />. The
/// <c>-X</c> command and the recipient after it are sent as their argv bytes, unconverted,
/// <c> SMTPUTF8</c> appended only to <c>EXPN</c>, in capitals, when advertised.</item>
/// <item>Every reply line is written to the output exactly as it arrived, line end included,
/// unless <c>-I</c> asked for no body: each continuation line as it arrives, and the final
/// line once the reply is accepted. A reply is accepted when it is 2xx, or 553 for a
/// command about a recipient; any other is exit 8, <c>Command failed: 550</c>, after which
/// no further recipient is tried and <c>QUIT</c> is still sent.</item>
/// <item>The replies written count against <c>--max-filesize</c>: the write that would pass
/// it is cut at the limit and the commands end with exit 63, <c>Exceeded the maximum allowed
/// file size (N) with N bytes</c>; no further recipient is tried and <c>QUIT</c> is still
/// sent (BL-1386).</item>
/// <item>The server closing before a reply is complete is exit 56, a reply line of 65536
/// bytes exit 100 and a reply line holding a NUL byte exit 8, none with <c>QUIT</c>.</item>
/// <item>Every result carries the bytes written as <see cref="TransferResult.BytesTransferred" />
/// and the last command reply's code as <see cref="TransferReport.ResponseCode" />;
/// <c>QUIT</c>'s reply changes neither.</item>
/// </list>
/// </remarks>
internal sealed class SmtpCommandTransfer(
    SmtpControlChannel channel, ITransferContext context, bool smtpUtf8Advertised, SmtpCommandLineText commandLineText)
{
    private const int AmbiguousRecipient = 553;

    private const string SmtpUtf8Keyword = " SMTPUTF8";

    private int responseCode = channel.LastReplyCode;

    private long written;

    /// <summary>
    /// Sends the command for each recipient in <paramref name="mail" />, or the one command
    /// when there is none, and closes the session.
    /// </summary>
    /// <param name="mail">The recipients and the <c>-X</c> command.</param>
    /// <returns>A success, or the failure that stopped the commands, with the report on it.</returns>
    public async ValueTask<TransferResult> SendAsync(MailRequestOptions mail)
    {
        TransferResult result;
        try
        {
            result = await SendCommandsWithinLimitAsync(mail).ConfigureAwait(false);
            if (result.ExitCode == CurlExitCode.Ok)
            {
                channel.Trace.DoingDone();
            }

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
        catch (SmtpSendFailedException failure)
        {
            result = TransferResult.Failure(CurlExitCode.SendError, failure.Message);
        }

        return result with
        {
            BytesTransferred = written,
            Report = new TransferReport { ResponseCode = responseCode },
        };
    }

    private async ValueTask<TransferResult> SendCommandsWithinLimitAsync(MailRequestOptions mail)
    {
        try
        {
            return await SendCommandsAsync(mail).ConfigureAwait(false);
        }
        catch (SmtpMaxFileSizeExceededException exceeded)
        {
            return TransferResult.Failure(CurlExitCode.FilesizeExceeded, exceeded.Message);
        }
    }

    private async ValueTask<TransferResult> SendCommandsAsync(MailRequestOptions mail)
    {
        string? custom = string.IsNullOrEmpty(mail.CustomCommand) ? null : commandLineText.ToWire(mail.CustomCommand);
        if (mail.Recipients.Count == 0)
        {
            return await ExchangeAsync(custom ?? "HELP", recipientCommand: false).ConfigureAwait(false)
                ?? TransferResult.Success(0);
        }

        foreach (string recipient in mail.Recipients)
        {
            string command = custom is null ? VerifyCommand(recipient) : CustomRecipientCommand(custom, recipient);
            if (await ExchangeAsync(command, recipientCommand: true).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }
        }

        return TransferResult.Success(0);
    }

    private string VerifyCommand(string recipient)
    {
        bool utf8 = smtpUtf8Advertised && SmtpMailbox.NeedsSmtpUtf8(recipient, commandLineText);
        return "VRFY " + SmtpMailbox.Bare(recipient, commandLineText) + (utf8 ? SmtpUtf8Keyword : string.Empty);
    }

    private string CustomRecipientCommand(string custom, string recipient)
    {
        bool utf8 = smtpUtf8Advertised && custom == "EXPN";
        return custom + " " + commandLineText.ToWire(recipient) + (utf8 ? SmtpUtf8Keyword : string.Empty);
    }

    /// <summary>
    /// Sends <paramref name="command" />, writes its reply, and returns the failure when the
    /// reply is not accepted.
    /// </summary>
    private async ValueTask<TransferResult?> ExchangeAsync(string command, bool recipientCommand)
    {
        await channel.SendAsync(command).ConfigureAwait(false);
        channel.Trace.CommandSent("COMMAND");
        SmtpReply reply = await channel.ReadReplyAsync(WriteAsync).ConfigureAwait(false) ?? throw new SmtpReplyMissingException();
        responseCode = reply.Code;
        if (!reply.IsCompletion && !(recipientCommand && reply.Code == AmbiguousRecipient))
        {
            return TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.CommandFailed("Command", reply.Code));
        }

        await WriteAsync(reply.FinalLine).ConfigureAwait(false);
        return null;
    }

    private async ValueTask WriteAsync(string line)
    {
        if (context.NoBody)
        {
            return;
        }

        byte[] bytes = Encoding.Latin1.GetBytes(line);
        long? limit = context.MaxFileSize;
        bool exceeds = written + bytes.Length > limit;
        if (exceeds)
        {
            bytes = bytes[..(int)(limit!.Value - written)];
        }

        await context.Output.WriteAsync(bytes, context.CancellationToken).ConfigureAwait(false);
        context.Events.ReportDataReceived(bytes);
        written += bytes.Length;
        context.Progress.ReportDownloaded(written, null);
        if (exceeds)
        {
            throw new SmtpMaxFileSizeExceededException(limit!.Value, written);
        }
    }
}
