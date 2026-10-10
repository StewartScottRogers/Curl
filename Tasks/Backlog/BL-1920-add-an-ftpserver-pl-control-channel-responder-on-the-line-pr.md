---
id: BL-1920
title: Add an ftpserver.pl control-channel responder on the line-protocol core
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1920 — Add an ftpserver.pl control-channel responder on the line-protocol core

## Goal

An `ILineProtocolResponder` stand-in for upstream's tests/ftpserver.pl control channel exists in Curl.Conformance.UnitLibrary and is unit tested, ready for BL-1905 to wire into UpstreamCaseRunner.

## Context

First slice of BL-1905, split off because the whole of BL-1905 does not fit one lane run's cost cap. Build on the line-protocol core of BL-1895 (ILineProtocolResponder.cs, LineProtocolReply.cs, LineProtocolServerCommands.cs, LineProtocolServerConnector.cs); read Curl.Conformance.UnitLibrary\CLAUDE.md first. Behaviour is that of tests/ftpserver.pl in the curl 8.21.0 tarball (https://curl.se/download/curl-8.21.0.tar.xz, read into an empty scratch folder, never the repository): the 220 greeting, default replies for USER, PASS, PWD, TYPE, CWD, SYST, QUIT and the like, per-command replies overridden by the case's `<servercmd>` REPLY lines, and a log of received commands in the form `<verify><protocol>` compares. No data connection (PASV/EPSV/PORT/EPRT, RETR, LIST) here: BL-1906 to BL-1908 add those. BCL only, opens no socket, platform-neutral.

## Acceptance criteria

- [ ] A responder class in Curl.Conformance.UnitLibrary sends the 220 greeting, answers the login and control commands with ftpserver.pl's default replies, and lets REPLY servercmd lines override a command's reply; unit tests in Curl.Conformance.UnitTests pin each.
- [ ] The responder records each received command line in the order and form ftpserver.pl writes to its protocol log.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-09: Created.
