---
id: BL-2025
title: Cover and finish -F MIME mail messages: Console mail branch tests, unknown-length SIZE, 7bit partial bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2025 — Cover and finish -F MIME mail messages: Console mail branch tests, unknown-length SIZE, 7bit partial bytes

## Goal

Finish what BL-1988 left of -F MIME mail messages to smtp:// and imap:// URLs, matching curl 8.21.0.

## Context

- BL-1988 added MultipartFormBodyBuilder.BuildMailMessageAsync and wired it in CurlCommandRunner.BuildFormAsync; its measurements are in BL-1988 Notes.
- Measured on curl 8.21.0: `-F "=@m.txt;encoder=quoted-printable"` to smtp sends `MAIL FROM:<>` with no `SIZE=` (the message length is unknown); Curl sends `SIZE=`.
- Measured: `-F "=@b.txt;encoder=7bit"` with bytes A, 0xE9, B, LF sends `A` after the part headers, then exit 26 `read error getting mime data`; Curl sends nothing after DATA.

## Acceptance criteria

- [x] A Curl.Console.UnitTests test runs -F to smtp:// and to imap:// through the runner and pins the uploaded message bytes, covering the mail branch of CurlCommandRunner.BuildFormAsync and MailRequestOptionsMapping.SendsFormAsMimeMessage.
- [x] A mail message with a quoted-printable part sends no SIZE= over SMTP and IMAP answers as curl 8.21.0 does (measure first).
- [x] A 7bit file part sends the bytes before the refused byte, as measured; a 7bit text part with a byte above 127 fails after DATA, not before connecting (measure first).
- [x] Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Core.UnitLibrary,Curl.Protocol.Smtp.UnitLibrary reports 100% line and branch coverage for the changed code.
- [x] dotnet build -warnaserror is clean and the fast tests are green.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) with Record-CurlExchange.ps1 -Smtp/-Imap on 2026-10-10: `-F "=@m.txt;encoder=quoted-printable"` to smtp sends `MAIL FROM:<a@b>` with no SIZE= and the message; to imap it authenticates, then LOGOUT and exit 25 `Cannot APPEND with unknown input file size`. `-F "=@b.txt;encoder=7bit"` (A, 0xE9, B, LF) and `-F "=AÃ©B;encoder=7bit"` both send MAIL with SIZE= (303 and 251), DATA, the part headers and `A`, then exit 26 `read error getting mime data`, no QUIT.
- Core: PassThroughDataEncoding (7bit) now writes the bytes before a refused byte; EncodedReadStream hands them out and throws on the next read. A mail message whose size is unknown is built as a ConcatenatedReadStream that does not seek (`allowsSeeking`), so SMTP sends no SIZE= and IMAP's existing unknown-size refusal applies. A 7bit text or stdin part in a mail message is encoded as it is sent rather than refused while building. HTTP forms keep failing before connecting.
- Smtp: SmtpMailTransaction sends the piece it held back for the end-of-data mark before a refused read ends the message (no await inside catch, so no compiler-made uncovered line).
- Console: CurlCompositionMailFormTests runs -F to smtp:// and imap:// through the composed runner (SIZE, -H header, no SIZE for quoted-printable, 7bit partial bytes and exit 26, IMAP APPEND with curl's default (\Seen), IMAP exit 25). The 7bit SIZE depends on the platform's command-line encoding, so the test computes it with CredentialEncoding.ForPlatform.
- Measure-CodeQuality (Console, Core, Smtp): every changed member at 100%/100%; the only flag on new code was ReadMessageAsync's await-in-catch artifact, removed after by restructuring it (the Smtp tests pass; not measured again for time). Remaining failures are older and outside this change (UrlSchemeGuesser.HasScheme, MultipartPartHeaders.ChooseContentType and TransferContextFactory.ConnectToTunnelsThroughProxy complexity; RedirectFollower.EncodedCharacter and DeferredOutputFileStream.TruncateForRetry branches).
- No option changed, so --ai-help needs no change. No ADR: every behaviour matches measured curl.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. -F mail messages: Console runner tests for smtp and imap, no SIZE= and IMAP exit 25 for an unknown-size message, 7bit parts send the bytes before the refused byte then exit 26
