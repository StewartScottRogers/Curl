---
id: BL-2179
title: Refuse control bytes in LDAP, SMB, TELNET, DICT, MQTT, GOPHER, RTSP and TFTP credentials with exit 43, as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests, Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2179 — Refuse control bytes in LDAP, SMB, TELNET, DICT, MQTT, GOPHER, RTSP and TFTP credentials with exit 43, as curl 8.22.0 does

## Goal

An LDAP, SMB, TELNET, DICT, MQTT, GOPHER, RTSP or TFTP user name or password holding a byte below 0x20 is exit 43, `control code detected in credentials`, before connecting, as curl 8.22.0 does.

## Context

- Found while doing BL-2177, which covered IMAP, POP3 and SMTP (BL-2162 covered FTP).
- Measured on curl 8.22.0 (Schannel build, 2026-10-13): `curl -u "a<TAB>b:p" <scheme>://127.0.0.1:1/x`
  is exit 43 for ldap, smb, telnet, dict, mqtt, gopher, rtsp and tftp (no connect attempt;
  port 1 would give exit 7), and exit 7 for http, https, ws and wss (`PROTOPT_USERPWDCTRL`).
  scp and sftp gave exit 2 on this build, so leave SSH out unless measured otherwise.
- Pattern to copy: `HasControlByte` and the check at the top of `TransferAsync` in
  `Curl.Protocol.Imap.UnitLibrary/ImapProtocolHandler.cs` (BL-2177).

## Acceptance criteria

- [x] A unit test per listed protocol pins exit 43, `control code detected in credentials`, and that nothing is sent.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Each of the eight handlers checks `ITransferContext.Credentials` first thing in its transfer (`RefuseControlBytes`, a private copy per library, since protocol libraries share nothing but Abstractions) and returns exit 43 `control code detected in credentials` before connecting, as BL-2177 did for IMAP. Telnet also logs the failure, as its other pre-session failures are logged.
- Tests: one `<Handler>CredentialControlCodeTests` per test project, with a counting connector: three control-byte cases pin exit 43 and zero connect attempts (so nothing is sent), and a clean-credentials case pins one connect, which also covers the false branches.
- Measure-CodeQuality.ps1 was not run: eight libraries at 30-45 minutes each do not fit the run; every branch of the one new expression is reached by the new tests or by the existing tests that pass no credentials.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. LDAP, SMB, TELNET, DICT, MQTT, GOPHER, RTSP and TFTP refuse a control byte in credentials with exit 43 before connecting
