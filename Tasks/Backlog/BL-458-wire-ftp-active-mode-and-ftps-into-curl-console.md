---
id: BL-458
title: Wire FTP active mode and ftps:// into Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-437, BL-456, BL-457]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-458 — Wire FTP active mode and ftps:// into Curl.Console

## Goal

`curl -P - ftp://host/f`, `curl --ssl-reqd ftp://host/f` and `curl ftps://host/f` work from the command line: the options reach the transfer context, `FtpProtocolHandler` is built with the listener and TLS provider, and `ftps` is routed and listed as curl 8.21.0 lists it.

## Context

- ADR-0102, "Contract additions", item 4. The handler work is BL-437, the listener BL-456, the options BL-457.
- `Curl.Console/TransferContextFactory.cs` maps `CommandLineOptions` to `TransferContext` (see `FtpDisableEpsv`); `Curl.Console/CurlComposition.cs` builds `new FtpProtocolHandler(connector)` and wraps it in `RoutingFtpProtocolHandler`, which serves only `ftp` today.
- `ftps` through an HTTP proxy: check how curl 8.21.0 treats it before routing it (ADR-0056, rule 3) and record the answer under Notes.
- `-V` lists protocols per ADR-0021; add `ftps` only as the platform's curl lists it.

## Acceptance criteria

- [ ] `TransferContextFactory` maps `FtpPort`, `FtpUseEprt`, `SslLevel` and `FtpSslControlOnly`; each pinned by a named test in `Curl.Console.UnitTests`.
- [ ] `CurlComposition` registers a handler for `ftps` and builds `FtpProtocolHandler` with `TcpConnectionListener` and the TLS provider; pinned by a named test.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for `Curl.Console`.

## Notes

Filed by BL-437 under ADR-0102.

## Log

- 2026-09-27: Created.
