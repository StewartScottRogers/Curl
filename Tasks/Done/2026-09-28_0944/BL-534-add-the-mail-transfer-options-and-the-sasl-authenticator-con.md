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
completed: 2026-09-28
---
# BL-534 — Add the mail transfer options and the SASL authenticator contract to Curl.Protocol.Abstractions

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` gains exactly the contract BL-533's ADR names: the mail members on `ITransferContext` (and `TransferContext`) and the SASL authenticator interface, documented, with defaults that leave every existing handler unchanged.

## Context

- Conformance audit 2026-09-28, rows 31 and 34. Design: BL-533's ADR; read it first and do not add anything it does not name.
- Files: `ITransferContext.cs`, `TransferContext.cs`, and a new file per new type. The ADR-0006 pattern (the context carries the Phase 4 protocol options) is the precedent.
- Contracts land before protocol work, so this task touches Abstractions alone.

## Acceptance criteria

- [x] Each member the ADR names exists with XML docs saying what it holds and which option fills it; `TransferContext` defaults are the "not given" values.
- [x] `Curl.Protocol.Abstractions.UnitTests` cover the defaults and any logic in the new types; the reference-graph test still passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: ADR-0121 §1 and §3 are the plan, taken verbatim. Added `ISaslAuthenticator`,
  `ISaslExchange`, `SaslRequest` (positional record, as `HttpAuthRequest`) and
  `MailRequestOptions` (sealed record with `init` members, as `HttpRequestOptions`), one file
  each, plus `ITransferContext.Mail`/`TransferContext.Mail` (null by default). Nothing the
  ADR does not name was added.
- The contract adds no behaviour, so the conformance stage has nothing to measure; the
  only `ITransferContext` implementation is `TransferContext`, so no handler changed.
- Tests: `MailRequestOptionsTests` (defaults, round trip, value equality),
  `SaslRequestTests` (round trip, nulls, value equality), and `TransferContextTests` pins
  `Mail` null by default and round-tripped. `ProtocolIsolationTests` still passes.
- Measured: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Abstractions
  575 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`
  100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ITransferContext.Mail (MailRequestOptions) and the ISaslAuthenticator, ISaslExchange and SaslRequest contract exist in Abstractions, documented and tested
