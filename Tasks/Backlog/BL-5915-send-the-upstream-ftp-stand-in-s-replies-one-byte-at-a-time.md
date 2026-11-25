---
id: BL-5915
title: Send the upstream FTP stand-in's replies one byte at a time under SLOWDOWN
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary/FtpControlChannelResponder.cs, Curl.Conformance.UnitLibrary/FtpServerConnector.cs, Curl.Conformance.UnitTests/FtpServerConnectorTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5915 — Send the upstream FTP stand-in's replies one byte at a time under SLOWDOWN

## Goal

For an FTP case whose `<servercmd>` says `SLOWDOWN`, the conformance harness's FTP stand-in sends each control-connection reply one byte per read, as upstream's `ftpserver.pl` does.

## Context

Found in BL-5905: upstream test 251 sat on the passing list while Curl hung against a server that really drips its replies, because only `DELAY` and `SLOWDOWNDATA` are carried out by the stand-in (`Curl.Conformance.UnitLibrary/FtpControlChannelResponder.cs`, `FtpServerConnector.cs`) and `SLOWDOWN` is ignored. Waits go on the stand-in's injected clock, as `SLOWDOWNDATA`'s do.

## Acceptance criteria

- [ ] A unit test in `Curl.Conformance.UnitTests` shows each reply of a `SLOWDOWN` server arriving one byte per read, with its waits on the injected clock.
- [ ] Every `SLOWDOWN` case on `PassingUpstreamCases.txt` still passes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
