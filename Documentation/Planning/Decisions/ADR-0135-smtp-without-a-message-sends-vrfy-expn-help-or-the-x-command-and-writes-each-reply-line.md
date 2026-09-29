# ADR-0135 — SMTP without a message sends VRFY, EXPN, HELP or the -X command and writes each reply line

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-543.

## Context

Without `-T`, or with `-T` but no `--mail-rcpt`, curl does not send mail: it sends one
command per recipient, or one command alone, and writes the server's reply to stdout.
Measured on Windows against curl 8.21.0 (mingw, Schannel) with
`Record-CurlExchange.ps1 -Smtp`, 2026-09-28, `-w '[%{response_code} %{size_download} %{size_upload}]'`
where shown:

| Case | curl sent | stdout | exit |
| --- | --- | --- | --- |
| `--mail-rcpt a@b` | `VRFY a@b` | `250 Recorder <recorder@localhost>\r\n` | 0 |
| `--mail-rcpt a@b --mail-rcpt c@d` | `VRFY a@b`, `VRFY c@d` | both replies | 0 |
| `--mail-rcpt '<a@b>' --mail-rcpt local` | `VRFY a@b`, `VRFY local` | both replies | 0 |
| `-X EXPN --mail-rcpt list` (EHLO offers `SMTPUTF8`) | `EXPN list SMTPUTF8` | `250-Alice <a@b>\r\n250 Bob <c@d>\r\n` | 0 |
| same, `EHLO` refused so `HELO` | `EXPN list` | reply, `[250 35 0]` | 0 |
| `-X expn --mail-rcpt list` | `expn list` | reply | 0 |
| `-X VRFY --mail-rcpt '<a@b>'` | `VRFY <a@b>` | reply | 0 |
| `-X NOOP --mail-rcpt a@b --mail-rcpt c@d` | `NOOP a@b`, `NOOP c@d` | `250 OK\r\n250 OK\r\n`, `[250 16 0]` | 0 |
| no option | `HELP` | the multiline 214 reply, every line | 0 |
| `-T mail.txt`, no `--mail-rcpt` | `HELP` | reply, `[214 74 0]` | 0 |
| `-X NOOP` | `NOOP` | `250 OK\r\n` | 0 |
| `-X NOOP`, reply `250-a\n250 b\r\n` | `NOOP` | `250-a\n250 b\r\n`, `[250 13 0]` | 0 |
| `-I -X NOOP` | `NOOP` | nothing, `[250 0 0]` | 0 |
| `VRFY` answered `550 no such user` | `VRFY x@y`, `QUIT` | nothing; stderr `curl: (8) Command failed: 550` | 8 |
| two recipients, first `VRFY` answered `550` | `VRFY a@b`, `QUIT` | nothing, `[550 0 0]` | 8 |
| `VRFY` answered `553 ambiguous` | `VRFY a@b` | `553 ambiguous\r\n`, `[553 15 0]` | 0 |
| `HELP` answered `550-first\r\n550 second` | `HELP`, `QUIT` | `550-first\r\n`, `[550 11 0]` | 8 |
| `-X 'FOO bar'` answered `502` | `FOO bar`, `QUIT` | nothing, `Command failed: 502` | 8 |
| `--mail-rcpt jörg@example.com` (ANSI argv) | `VRFY j\xF6rg@example.com SMTPUTF8` | reply | 0 |
| `--mail-rcpt a@bücher.example` | `VRFY a@xn--bcher-kva.example SMTPUTF8` | reply | 0 |
| `-X ''` | nothing; the command line refuses a blank `-X` | | 2 |

This matches curl's `smtp_perform_command` and `smtp_state_command_resp`: continuation lines
are separate responses in the command state and are written as they arrive, the final line is
written only when accepted (2xx, or 553 for a command about a recipient), and the first
refusal stops the loop.

## Decision

`SmtpCommandTransfer` does exactly what the table shows:

1. With an upload and at least one recipient the message is sent (BL-542); otherwise the
   commands are.
2. No `-X` (or an empty one, which the command line never passes) sends `VRFY` for each
   recipient, or `HELP` when there is none. `VRFY` strips one leading `<` and one trailing
   `>`, converts the host part to an IDNA A-label with the BCL's `IdnMapping` (sending it as
   given when there is none), and appends ` SMTPUTF8` when the `EHLO` reply offered
   `SMTPUTF8` (matched case-sensitively after the code, as curl does) and the address is not
   all ASCII.
3. `-X` with recipients sends `<command> <recipient>` for each, the recipient as given, and
   ` SMTPUTF8` only when the command is exactly `EXPN` and `SMTPUTF8` was offered; `-X`
   alone sends the command as given.
4. Each continuation line goes to the output with its line end as it arrives; the final line
   follows once the reply is accepted; `-I` writes nothing. A refusal is exit 8
   `Command failed: <code>` and still sends `QUIT`; a closed connection (56) and an overlong
   line (100) send nothing more.
5. `%{size_download}` is the bytes written and `%{response_code}` the last command reply's
   code; `QUIT`'s reply changes neither.
6. Amended by BL-776 (2026-09-29): addresses in `MAIL FROM`, `AUTH=`, `RCPT TO` and `VRFY`,
   and the `-X` command with its recipient, go out as curl's argv bytes
   (`SmtpCommandLineText`): the system ANSI code page with best fit on Windows (measured on
   code page 1252: `ö` is `F6`, `€` is `80`, `ł` is `l`) and UTF-8 on Linux and macOS
   (curl's source sends argv as given). The host part is converted to an IDNA A-label from
   the text as curl received it, and `SMTPUTF8` is added when those bytes are not all ASCII,
   so a best-fitted `łx@ł.example` is `<lx@l.example>` without it (measured). The handler
   picks the host's encoding itself, as `Curl.Console` passes it nothing for SMTP. Commands
   that do not come from the command line stay Latin-1.

## Consequences

- `smtp://host/` alone now asks for `HELP` and prints it, as curl does, instead of closing
  the session in silence.
- `SmtpReply` carries its final line as it arrived and `SmtpControlChannel.ReadReplyAsync`
  hands out continuation lines as they arrive, so the output bytes are the server's bytes.
- `smtpUtf8Advertised` comes from the last accepted `EHLO`; curl keeps the flag from the
  first `EHLO` even if the one after `STARTTLS` drops it. No server is known to do that.

## Alternatives considered

- **Write the whole reply after it completes.** Simpler, but a refused multiline reply would
  print nothing where curl prints its continuation lines (measured `550-first\r\n`).
- **Send UTF-8 for non-ASCII recipients.** Right for Linux and macOS, wrong for the measured
  Windows build; BL-776 sends each platform's argv bytes instead.
- **Carry the argv encoding on `MailRequestOptions`, as `HttpRequestOptions` does.**
  Rejected for BL-776: it changes the shared contract in `Curl.Protocol.Abstractions` for
  one protocol's use; the handler chooses the same encoding from the host it runs on.
