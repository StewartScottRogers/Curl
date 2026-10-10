---
id: BL-1925
title: Answer SMTP commands as ftpserver.pl does (SMTP line-protocol responder)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1925 — Answer SMTP commands as ftpserver.pl does (SMTP line-protocol responder)

## Goal

An `SmtpResponder : ILineProtocolResponder` in Curl.Conformance.UnitLibrary answers SMTP the way upstream's `tests/ftpserver.pl` (curl-8_21_0) does, ready for BL-1909 to wire into the case runner.

## Context

Split from BL-1909 (lane 2, 2026-10-09) so each piece fits one lane run's $2 cost cap, as BL-1920 was split from BL-1905. Read Curl.Conformance.UnitLibrary\CLAUDE.md first. Build on the line-protocol core of BL-1895: `ILineProtocolResponder`, `LineProtocolReply`, `LineProtocolServerCommands`, `LineProtocolServerConnector`; `FtpControlChannelResponder` is the model to follow (command splitting, `REPLY` lines from `<servercmd>`, `ReceivedCommandLines` for `<verify><protocol>`). Behaviour from the smtp parts of tests/ftpserver.pl in the tarball (https://curl.se/download/curl-8.21.0.tar.xz, read into an empty scratch folder, never the repository): the greeting, EHLO/HELO (with `<servercmd>` CAPA and AUTH lines), MAIL FROM, RCPT TO, DATA with the dot terminator, RSET, VRFY, EXPN, NOOP, QUIT, AUTH exchanges as the script does them, and replies from `<reply>` parts and `REPLY` lines. The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (test9xx are SMTP); use them to pick test inputs. The responder records the DATA message (dot-unstuffed, without the terminator) for `<verify><upload>`.

## Acceptance criteria

- [ ] `SmtpResponder` answers greeting, EHLO, HELO, MAIL, RCPT, DATA (through the `.` line), RSET, VRFY, EXPN, NOOP, QUIT and AUTH as ftpserver.pl does, each pinned by a unit test in Curl.Conformance.UnitTests quoting the script's reply text.
- [ ] `<servercmd>` `REPLY` lines override a command's answer, as for FTP.
- [ ] The DATA message is exposed for `<verify><upload>` comparison, pinned by a test.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests are platform-neutral, no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
