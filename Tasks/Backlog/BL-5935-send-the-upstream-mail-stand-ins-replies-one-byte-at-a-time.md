---
id: BL-5935
title: Send the upstream mail stand-ins' replies one byte at a time under SLOWDOWN
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5924]
touches: [Curl.Conformance.UnitLibrary/LineProtocolServerConnector.cs, Curl.Conformance.UnitLibrary/Pop3ServerConnector.cs, Curl.Conformance.UnitLibrary/ImapServerConnector.cs, Curl.Conformance.UnitLibrary/SmtpServerConnector.cs, Curl.Conformance.UnitLibrary/FtpServerConnector.cs, Curl.Conformance.UnitLibrary/OneBytePerReadConnection.cs, Curl.Conformance.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitTests/Pop3ServerConnectorTests.cs, Curl.Conformance.UnitTests/ImapServerConnectorTests.cs, Curl.Conformance.UnitTests/SmtpServerConnectorTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5935 — Send the upstream mail stand-ins' replies one byte at a time under SLOWDOWN

## Goal

For a POP3, IMAP or SMTP case whose `<servercmd>` says `SLOWDOWN`, the conformance harness's mail stand-in gives curl its greeting and each reply one byte per read, as upstream's `ftpserver.pl` does.

## Context

BL-5924 did this for the FTP stand-in only: `FtpServerConnector` wraps each control connection in `OneBytePerReadConnection` (10 ms a byte) when `<servercmd>` has a `SLOWDOWN` line. Upstream's `ftpserver.pl` serves the mail protocols too, and test 853 (`pop3`, on `PassingUpstreamCases.txt`) says `SLOWDOWN`, which `Pop3ServerConnector` ignores. Move the `SLOWDOWN` line check and the wrapping where the four stand-ins share them (`LineProtocolServerConnector`). Waits go on each stand-in's injected clock.

## Acceptance criteria

- [ ] A unit test in `Curl.Conformance.UnitTests` shows a `SLOWDOWN` POP3, IMAP and SMTP server's replies arriving one byte per read, with their waits on the injected clock.
- [ ] Every `SLOWDOWN` case on `PassingUpstreamCases.txt` still passes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
