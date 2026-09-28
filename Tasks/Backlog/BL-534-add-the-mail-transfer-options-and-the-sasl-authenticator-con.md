---
id: BL-534
title: Add the mail transfer options and the SASL authenticator contract to Curl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-533]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-534 — Add the mail transfer options and the SASL authenticator contract to Curl.Protocol.Abstractions

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` gains exactly the contract BL-533's ADR names: the mail members on `ITransferContext` (and `TransferContext`) and the SASL authenticator interface, documented, with defaults that leave every existing handler unchanged.

## Context

- Conformance audit 2026-09-28, rows 31 and 34. Design: BL-533's ADR; read it first and do not add anything it does not name.
- Files: `ITransferContext.cs`, `TransferContext.cs`, and a new file per new type. The ADR-0006 pattern (the context carries the Phase 4 protocol options) is the precedent.
- Contracts land before protocol work, so this task touches Abstractions alone.

## Acceptance criteria

- [ ] Each member the ADR names exists with XML docs saying what it holds and which option fills it; `TransferContext` defaults are the "not given" values.
- [ ] `Curl.Protocol.Abstractions.UnitTests` cover the defaults and any logic in the new types; the reference-graph test still passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
