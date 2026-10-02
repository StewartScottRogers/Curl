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
completed:
---
# BL-1163 — Write the --trace-config smtp lines from the SMTP state machine

## Goal

Under `-v --trace-config smtp` (and `protocol`, `all`) Curl writes curl 8.21.0's `* [SMTP] ...` lines from its SMTP handler, at the same steps and with the same text.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` (`CurlComposition`) decides whether the component is on (`smtp`, `protocol` or `all` in `CommandLineOptions.TraceComponents`) and hands the SMTP handler an `ITransferEvents` sink; the SMTP library writes each line at its own step.
- Measured lines are in Notes.

## Acceptance criteria

- [ ] A one-recipient send under `-v --trace-config smtp` writes the measured `[SMTP]` lines in Notes, in that order, between the usual `-v` lines; a test pins them.
- [ ] `protocol` and `all` write the same lines; `-v` alone, another component, and `smtp` without `-v` write none (tests).
- [ ] The `cr_eob_read` lengths follow the body actually read (the measured 18 is the 18-byte body).
- [ ] `--ai-help` still describes `--trace-config` correctly.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

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

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
