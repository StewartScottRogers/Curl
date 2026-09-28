# ADR-0136 — SMTP MAIL FROM adds AUTH=, SIZE= and SMTPUTF8, and --mail-rcpt-allowfails carries on past refused recipients

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-544.

## Context

BL-542 sent `MAIL FROM:<addr>` and stopped at the first refused `RCPT`. curl adds parameters
to `MAIL FROM` and, under `--mail-rcpt-allowfails`, keeps going. Measured on Windows against
curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1 -Smtp`, 2026-09-28, curl running
`-sS --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:18025/h` with the 21-byte
`Subject: t\r\n\r\nhello\r\n` and the options below. The recorder's `EHLO` advertises
`AUTH PLAIN LOGIN CRAM-MD5`, `STARTTLS`, `SIZE 1000000`, `8BITMIME` and `SMTPUTF8`.

| Case | curl sent | exit, stderr |
| --- | --- | --- |
| `--mail-auth x@y`, no `-u` | `MAIL FROM:<a@b> SIZE=21` | 0 |
| `-u u:p --mail-auth x@y` | `AUTH CRAM-MD5` ..., `MAIL FROM:<a@b> AUTH=<x@y> SIZE=21` | 0 |
| `-u u:p --mail-auth '<x@y>'` | `MAIL FROM:<a@b> AUTH=<x@y> SIZE=21` | 0 |
| `-u u:p --mail-auth xy`, no `--mail-from` | `MAIL FROM:<> AUTH=<xy> SIZE=21` | 0 |
| `-u u:p --mail-auth x@yü.de` | `MAIL FROM:<> AUTH=<x@xn--y-eha.de> SIZE=21 SMTPUTF8` | 0 |
| `-u u:p --mail-auth xü@y` | `MAIL FROM:<a@b> AUTH=<xü@y> SIZE=21 SMTPUTF8` | 0 |
| `--mail-auth ''` | nothing | 2, blank argument |
| `-T -` | `MAIL FROM:<a@b>` | 0 |
| `-T empty.txt` | `MAIL FROM:<a@b>` | 0 |
| `EHLO` offers no `SIZE` | `MAIL FROM:<a@b>` | 0 |
| `EHLO` offers `size 100`, `SIZE`, or `SIZEX` | `MAIL FROM:<a@b> SIZE=21` | 0 |
| `EHLO` refused, so `HELO` | `MAIL FROM:<a@b>` | 0 |
| `--mail-from aü@b --mail-rcpt cü@dü.de` | `MAIL FROM:<aü@b> SIZE=21 SMTPUTF8`, `RCPT TO:<cü@xn--d-eha.de>` | 0 |
| same, `EHLO` without `SMTPUTF8` | `MAIL FROM:<aü@b> SIZE=21`, `RCPT TO:<cü@xn--d-eha.de>` | 0 |
| `--mail-from a@bü.de` | `MAIL FROM:<a@xn--b-eha.de> SIZE=21 SMTPUTF8` | 0 |
| `--mail-rcpt cü@d` only | `MAIL FROM:<a@b> SIZE=21 SMTPUTF8`, `RCPT TO:<cü@d>` | 0 |
| `EHLO` offers `smtputf8`, `--mail-from aü@b` | `MAIL FROM:<aü@b> SMTPUTF8` | 0 |
| same, no upload, `--mail-rcpt jörg@x` | `VRFY jörg@x SMTPUTF8` | 0 |
| two recipients, first `RCPT` 550 | `RCPT TO:<c@d>`, `QUIT` | 55, `RCPT failed: 550` |
| same with `--mail-rcpt-allowfails` | both `RCPT`s, `DATA`, the message | 0 |
| allowfails, second refused 551 | both `RCPT`s, `DATA`, the message | 0 |
| allowfails, first 450, second accepted | both `RCPT`s, `DATA`, the message | 0 |
| allowfails, both refused (550, 551) | both `RCPT`s, `QUIT` | 55, `RCPT failed: 551 (last error)` |
| allowfails, one accepted, `DATA` 554 | ..., `DATA`, `QUIT` | 55, `DATA failed: 554` |
| allowfails, second `RCPT` unanswered | both `RCPT`s | 56, `response reading failed (errno: 0)` |

`ü` went out as the single byte `FC`: curl sends its argv bytes, the ANSI code page on Windows.

## Decision

1. `MAIL FROM:<from>` is followed, in this order, by ` AUTH=<addr>` when `--mail-auth` was
   given and an `AUTH` exchange succeeded in this session; ` SIZE=<n>` when `EHLO`
   advertised `SIZE` and the upload can seek and has `n > 0` bytes left; and ` SMTPUTF8` when
   `EHLO` advertised `SMTPUTF8` and the reverse path, the `AUTH=` address actually sent, or any
   recipient holds a character outside ASCII.
2. Every mailbox in `MAIL FROM`, `AUTH=` and `RCPT TO` loses one leading `<` and one trailing
   `>` and has the part after its first `@` made an IDNA A-label with the BCL's `IdnMapping`,
   whether or not `SMTPUTF8` was advertised (`SmtpMailbox`, shared with `VRFY`). An empty
   `--mail-auth`, which the command line refuses, is `AUTH=<>` as curl's source writes it.
3. `STARTTLS`, `SIZE` and `SMTPUTF8` are matched after the reply code in any case and as a
   prefix of the line (`SmtpReply.Advertises`). This corrects ADR-0135 decision 2, which
   matched `SMTPUTF8` case-sensitively without measuring it: lowercase `smtputf8` was
   measured to count for `VRFY` too.
4. Under `--mail-rcpt-allowfails` a refused `RCPT` is passed over. When at least one was
   accepted, `DATA` follows; when all were refused the result is exit 55
   `RCPT failed: <last code> (last error)`, `QUIT` is sent and `%{response_code}` is that last
   code. Without the option the first refusal stops the message as before.
5. Commands stay Latin-1, as ADR-0135 decision 6 sets out: a non-ASCII local part goes out as
   its Latin-1 byte, which is the Windows build's byte for characters up to U+00FF.

## Consequences

- An authenticated upload tells the server the submitting identity, and a server with a size
  limit can refuse an oversized message before the data is sent, as with curl.
- `SmtpSaslAuthentication.IsAuthenticated` tells the session whether `AUTH=` may be sent;
  a session whose `EHLO` did not offer `AUTH`, or that had no credentials, sends none.
- `SIZE=` is the bytes left in the upload, not the dot-stuffed count, matching curl's
  `infilesize`.

## Alternatives considered

- **Send `AUTH=` whenever `--mail-auth` is given.** Measured wrong: without `-u` curl sends no
  `AUTH=`.
- **Count `SIZE` from the stuffed message.** curl sends the file's length (21 for the 21-byte
  file), so the stuffed count would differ whenever a line starts with a dot.
- **Keep `SMTPUTF8` case-sensitive for `VRFY`.** Measured wrong.
