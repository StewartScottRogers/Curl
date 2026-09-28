---
id: BL-459
title: Add IConnectionListener, IConnection.LocalEndPoint and the FTP active-mode and TLS transfer options to the abstractions
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-459 — Add IConnectionListener, IConnection.LocalEndPoint and the FTP active-mode and TLS transfer options to the abstractions

## Goal

`Curl.Protocol.Abstractions` has the contract ADR-0102 names, so `FtpProtocolHandler` (BL-437) can listen for an active-mode data connection, learn its control connection's local address, and read `-P`, `--disable-eprt`, `--ssl`/`--ssl-reqd` and `--ftp-ssl-control` from the transfer context.

## Context

- ADR-0102 (`Documentation/Planning/Decisions/ADR-0102-ftp-active-mode-and-tls-need-a-listening-seam-and-four-transfer-options.md`), "Contract additions", item 1, is the specification.
- `IConnector.cs`, `ConnectTarget.cs`, `ConnectResult.cs` and `IConnection.cs` show the existing seam style (results returned, only `OperationCanceledException` thrown; validated records).
- `ITransferContext.cs` / `TransferContext.cs` show how FTP options were added before (`FtpDisableEpsv`, `FtpSkipPasvIp`, ADR-0006).
- Adding a default interface member to `IConnection` must leave every existing implementer and test fake compiling unchanged.

## Acceptance criteria

- [ ] `IConnectionListener`, `ListenTarget`, `ListenResult` and `IPendingConnection` exist in `Curl.Protocol.Abstractions` as ADR-0102 describes, with XML doc comments; `ListenTarget` rejects a port outside 0-65535 and a range whose low end exceeds its high end, each pinned by a named test.
- [ ] `IConnection.LocalEndPoint` is a default interface member returning `null`; a named test shows a fake that does not override it returns `null`.
- [ ] `ITransferContext` and `TransferContext` carry `FtpPort` (`null` default), `FtpUseEprt` (`true` default), `SslLevel` (`TransportSecurityLevel.None` default) and `FtpSslControlOnly` (`false` default), each default pinned by a named test.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for the new members.

## Notes

Filed by BL-437 under ADR-0102. BL-437 depends on this task.

## Log

- 2026-09-27: Created.
