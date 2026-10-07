---
id: BL-1163
title: Write the --trace-config smtp lines from the SMTP state machine
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1163 — Write the --trace-config smtp lines from the SMTP state machine

## Goal

Under `-v --trace-config smtp` (and `protocol`, `all`) Curl writes curl 8.21.0's `* [SMTP] ...` lines from its SMTP handler, at the same steps and with the same text.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` (`CurlComposition`) decides whether the component is on (`smtp`, `protocol` or `all` in `CommandLineOptions.TraceComponents`) and hands the SMTP handler an `ITransferEvents` sink; the SMTP library writes each line at its own step.
- Measured lines are in Notes.

## Acceptance criteria

- [x] A one-recipient send under `-v --trace-config smtp` writes the measured `[SMTP]` lines in Notes, in that order, between the usual `-v` lines; a test pins them.
- [x] `protocol` and `all` write the same lines; `-v` alone, another component, and `smtp` without `-v` write none (tests).
- [x] The `cr_eob_read` lengths follow the body actually read (the measured 18 is the 18-byte body).
- [x] `--ai-help` still describes `--trace-config` correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Measured 2026-10-02 (BL-1104), curl 8.21.0 Schannel,
`Record-CurlExchange.ps1 -Smtp -StandardInput 'Subject: x\r\n\r\nhi\r\n' -CurlArgs '-sS','--trace-config','smtp','-v','--mail-from','a@b','--mail-rcpt','c@d','-T','-','smtp://127.0.0.1:P/'`, the `[SMTP]` lines in order:

```
* [SMTP] smtp_setup_connection() -> 0
* [SMTP] state change from STOP to SERVERGREET
* [SMTP] state change from SERVERGREET to EHLO
* [SMTP] state change from EHLO to STOP
* [SMTP] smtp_perform(), start
* [SMTP] state change from STOP to MAIL
* [SMTP] smtp_perform() -> 0, connected=1, done=0
* [SMTP] smtp_regular_transfer() -> 0, done=0
* [SMTP] smtp_do() -> 0, done=0
* [SMTP] smtp_doing() -> 0, done=0
* [SMTP] state change from MAIL to RCPT
* [SMTP] smtp_doing() -> 0, done=0
* [SMTP] state change from RCPT to DATA
* [SMTP] smtp_doing() -> 0, done=0
* [SMTP] state change from DATA to STOP
* [SMTP] smtp_doing() -> 0, done=1
* [SMTP] cr_eob_read, next_read(len=65536) -> 0, 18 eos=0
* [SMTP] cr_eob_read, next_read(len=65536) -> 0, 0 eos=1
* [SMTP] auto-ending mail body with '\r\n.\r\n'
* [SMTP] mail body complete, returning EOS
* [SMTP] state change from STOP to POSTDATA
* [SMTP] state change from POSTDATA to STOP
* [SMTP] smtp_done(status=0, premature=0) -> 0
```

Re-record the full stderr to place them among the `-v` lines, and measure AUTH and a refused recipient.

Done 2026-10-02 (ADR-0368). Re-recorded the full stderr for the send above, `-u u:p` (AUTH
CRAM-MD5), `-SmtpReply 'RCPT=550 no such user'`, `-SmtpReply 'DATA=250 OK'`, a body without a
final CRLF and a `VRFY` (`--mail-rcpt c@d`, no `-T`). What they showed:

- `smtp_setup_connection` comes before `Trying`; `STOP to SERVERGREET` after `Established
  connection`; each state change straight after its command's `>` line.
- AUTH: `EHLO to AUTH` after `> AUTH CRAM-MD5`, nothing for the challenge, `AUTH to STOP` after 235.
- Refused RCPT/DATA: `* RCPT failed: 550`, then `smtp_doing() -> 55, done=0`,
  `smtp_done(status=55, premature=0) -> 55`, then `shutting down connection #0`.
- VRFY: state `COMMAND`; after the reply and its `{ [35 bytes data]`, `COMMAND to STOP`,
  `smtp_doing() -> 0, done=1`, `smtp_done(...) -> 0`.
- Under the trace the body and the mark are separate data events, `} [18 bytes data]` after
  the 18-byte read line and `} [3 bytes data]` (`[5 bytes]` without a final CRLF) after
  `mail body complete`; the `auto-ending` text is the same either way.
- `--trace-config all` adds the time and id prefixes, so the byte-level runner test covers
  `smtp` and `protocol`; `all` is pinned at `CurlComposition.TracesSmtp`.

Curl's own run matched curl's stderr byte for byte (ports masked) for all six. Not measured,
taken from curl's state names (ADR-0368 point 4): `HELO`, `STARTTLS`, `UPGRADETLS`; no
doing/done lines for a failure before the DO phase; `status=0` for a refused message.

Found and filed: BL-1198 - without the trace, a `-T -` upload writes `} [21 bytes data]` where
curl writes `} [18 bytes data]` (pre-existing, not this task's).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config smtp/protocol/all writes curl 8.21.0's [SMTP] state machine lines, matching real curl byte for byte
