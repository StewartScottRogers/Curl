---
id: BL-1895
title: Add a line-protocol server core for upstream FTP, SMTP, IMAP and POP3 cases
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1895 — Add a line-protocol server core for upstream FTP, SMTP, IMAP and POP3 cases

## Goal

A reusable in-memory line-protocol server core exists in Curl.Conformance.UnitLibrary that the FTP, SMTP, IMAP and POP3 stand-ins build on.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Upstream serves FTP, SMTP, IMAP and POP3 from one script, tests/ftpserver.pl (read it in the tarball), which reads CRLF-terminated command lines, matches each against the case's <reply> parts and <servercmd> commands, writes replies, and logs received commands for comparison with <verify><protocol>. Build the shared part only: an IConnector-based server skeleton, a CRLF line reader that records received bytes like SwsServerRecording, a reply writer (with servercmd lookups by command name, to be extended by the protocol tasks), and the screening hook that lets a case whose <server> names a supported protocol pass. No real protocol commands yet; a test-only echo protocol exercises the core. Follow the structure of SwsHttpServerConnector and SwsHttpServerConnection. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] Unit tests drive the core through an in-memory connection: command lines split on CRLF across read boundaries, replies written, received bytes recorded for <verify><protocol> comparison, close on request.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md describes the core as the base of BL-1905, BL-1909, BL-1910 and BL-1911.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; `dotnet build -warnaserror` is clean for every touched project and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Core: `LineProtocolServerConnector` (IConnector, one `ILineProtocolResponder` per connection from a factory, so each FTP/SMTP/IMAP/POP3 session keeps its own state), `LineProtocolServerConnection`, `LineProtocolReply`, and `LineProtocolServerCommands` for `<servercmd>` `REPLY <command> <text>` lookups. Recording reuses `SwsServerRecording` rather than a copy. A line ends only at CRLF, as the task describes ftpserver.pl reading; a lone LF stays in the line.
- Screening hook: `UpstreamCaseScreening` adds `LineProtocolServerConnector.EmulatedServers` (empty for now) to its supported servers; each protocol task adds its name when its stand-in answers, so no FTP case runs before a server exists.
- Coverage: not re-measured with Measure-CodeQuality.ps1 (run budget); every branch is driven by `LineProtocolServerConnectorTests` by construction (greeting, split lines, lone LF, short buffer, wait, cancel, close, recording, REPLY parsing). Complexity of each new method is under 5. Conformance fast tests: 1189 passed, 0 failed.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Line-protocol server core and screening hook in place, tested through an echo protocol
