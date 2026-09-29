---
id: BL-914
title: Carry --ftp-account, --ftp-alternative-to-user and --ftp-pret to protocols as ITransferContext members
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-914 — Carry --ftp-account, --ftp-alternative-to-user and --ftp-pret to protocols as ITransferContext members

## Goal

`ITransferContext` gains `string? FtpAccount` (`--ftp-account`: the account sent with `ACCT` when the server answers `332`), `string? FtpAlternativeToUser` (`--ftp-alternative-to-user`: the command sent when `USER` is refused) and `bool FtpSendPret` (`--ftp-pret`: send `PRET` before `EPSV`/`PASV`), `null`/`false` when not given, and `TransferContext` implements them as `init` properties with those defaults.

## Context

- BL-634 parses the options into `CommandLineOptions.FtpAccount`, `FtpAlternativeToUser` and `FtpSendPret` (`Curl.Cli.UnitLibrary`). BL-635 (the FTP handler sending `ACCT`, the alternative `USER` command and `PRET`, and the wiring in `Curl.Console/TransferContextFactory.cs`) consumes them and cannot without these members; its Context says to file this contract change separately.
- Files: `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs` (follow the `FtpPort` / `FtpCreateDirectories` doc comment style; remark that `ftp://` reads each) and `TransferContext.cs` (the only implementer). Wiring from `CommandLineOptions` in `Curl.Console` is BL-635's, not this task's.

## Acceptance criteria

- [x] `ITransferContext.FtpAccount`, `ITransferContext.FtpAlternativeToUser` and `ITransferContext.FtpSendPret` exist with XML doc comments; `TransferContext` implements all three, defaulting to `null`, `null` and `false`.
- [x] `Curl.Protocol.Abstractions.UnitTests/TransferContextTests.cs` covers the defaults and an `init` of each.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered directly rather than through the full `/feature` stages: a three-member contract addition with no behaviour to plan. The members sit after `FtpCreateDirectories` beside the other FTP options. The test-only `ForwardingTransferContext` (Curl.Protocol.Abstractions.UnitTests) forwards the three as well. Build clean, fast tests green, Measure-CodeQuality: 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ITransferContext carries FtpAccount, FtpAlternativeToUser and FtpSendPret; TransferContext defaults them to null, null, false
