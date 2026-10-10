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
completed: 2026-10-09
---
# BL-1920 — Add an ftpserver.pl control-channel responder on the line-protocol core

## Goal

An `ILineProtocolResponder` stand-in for upstream's tests/ftpserver.pl control channel exists in Curl.Conformance.UnitLibrary and is unit tested, ready for BL-1905 to wire into UpstreamCaseRunner.

## Context

First slice of BL-1905, split off because the whole of BL-1905 does not fit one lane run's cost cap. Build on the line-protocol core of BL-1895 (ILineProtocolResponder.cs, LineProtocolReply.cs, LineProtocolServerCommands.cs, LineProtocolServerConnector.cs); read Curl.Conformance.UnitLibrary\CLAUDE.md first. Behaviour is that of tests/ftpserver.pl in the curl 8.21.0 tarball (https://curl.se/download/curl-8.21.0.tar.xz, read into an empty scratch folder, never the repository): the 220 greeting, default replies for USER, PASS, PWD, TYPE, CWD, SYST, QUIT and the like, per-command replies overridden by the case's `<servercmd>` REPLY lines, and a log of received commands in the form `<verify><protocol>` compares. No data connection (PASV/EPSV/PORT/EPRT, RETR, LIST) here: BL-1906 to BL-1908 add those. BCL only, opens no socket, platform-neutral.

## Acceptance criteria

- [x] A responder class in Curl.Conformance.UnitLibrary sends the 220 greeting, answers the login and control commands with ftpserver.pl's default replies, and lets REPLY servercmd lines override a command's reply; unit tests in Curl.Conformance.UnitTests pin each.
- [x] The responder records each received command line in the order and form ftpserver.pl writes to its protocol log.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Behaviour read from tests/ftpserver.pl in the curl-8.21.0 tarball (scratch folder outside the repo): display texts, banner, `500 <cmd> is not dealt with!`, `500 Unrecognized command` + close for a line not matching `^([A-Z]{3,4})(\s(.*))?$`, PWD_ftp/CWD_ftp/switch_directory.
- Kept ftpserver.pl's quirks: display text is looked up by the command as written and the handler by its upper case, so `cwd x` moves the directory and sends nothing, `user` gets the not-dealt-with answer; `..` strips only an alphanumeric last segment.
- Choices: `REPLY` lookup reuses `LineProtocolServerCommands` (case-insensitive, CRLF appended; ftpserver.pl's `REPLYLF`, quoted full-text `REPLY "CMD ARG"`, `COUNT` and `DELAY` are not read yet - left for BL-1905 when cases need them). The protocol log is `ReceivedCommandLines` (each line + CRLF, as ftpserver.pl writes server.input); the connector's `ReceivedBytes` already records the raw bytes across connections. Commands with data-connection handlers (PASV, EPSV, RETR, SIZE, MDTM, STOR...) answer only by REPLY/display text/not-dealt-with until BL-1906..BL-1908.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. FtpControlChannelResponder answers ftpserver.pl's control channel; 100% coverage, build clean, fast tests green
