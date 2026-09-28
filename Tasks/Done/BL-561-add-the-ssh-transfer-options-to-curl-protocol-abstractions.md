---
id: BL-561
title: Add the SSH transfer options to Curl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-560]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-561 — Add the SSH transfer options to Curl.Protocol.Abstractions

## Goal

`ITransferContext` and `TransferContext` gain exactly the SSH members BL-560's ADR names (private and public key paths and passphrase, known-hosts path, expected host key MD5 and SHA-256, compression), documented, with defaults that leave every existing handler unchanged.

## Context

- Conformance audit 2026-09-28, rows 31 and 35. Design: BL-560's ADR; add nothing it does not name.
- Files: `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`, `TransferContext.cs`. Existing members (`Credentials`, `QuoteCommands`, `FtpCreateDirectories`, `ResumeFrom`, `Range`) are reused by SFTP, not duplicated.
- Contracts land before protocol work, so this task touches Abstractions alone.

## Acceptance criteria

- [x] Each new member has XML docs naming the option that fills it; `TransferContext` defaults are the "not given" values.
- [x] `Curl.Protocol.Abstractions.UnitTests` cover the defaults; the reference-graph test still passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered directly rather than through the full /feature agent chain: the design is fixed by ADR-0122 ("Transfer options"), and the change is one record plus one property. `SshOptions` (sealed record, seven members exactly as ADR-0122 names them) hangs off `ITransferContext.Ssh`, null outside scp/sftp like `Http` and `Mail`. Defaults are all null/false, so no existing handler changes.
- `RedirectFollower` builds hop contexts without `Mail` too; redirects are HTTP-only, so `Ssh` is not carried either.
- Verified: `dotnet build Curl.slnx -warnaserror` clean, fast tests green (Abstractions 578 passed incl. `SshOptionsTests` and `ProtocolIsolationTests`), `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ITransferContext.Ssh carries SshOptions (key, pubkey, pass, known hosts, host key MD5/SHA-256, compression) per ADR-0122
