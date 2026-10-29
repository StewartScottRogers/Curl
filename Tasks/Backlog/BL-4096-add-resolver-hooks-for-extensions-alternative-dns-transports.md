---
id: BL-4096
title: Add resolver hooks for extensions: alternative DNS transports
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Resolution, Curl.Extension.Abstractions.UnitTests/Resolution, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4096 — Add resolver hooks for extensions: alternative DNS transports

## Goal

An extension activated by its option can supply the `IDnsResolver` a transfer uses, e.g. DNS-over-TLS, DNS-over-QUIC or Oblivious DoH.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Needed by DoT/DoQ, Oblivious DoH and DNSSEC/DANE.

## Acceptance criteria

- [ ] A test extension's resolver answers one fake transfer's lookup (engine unit test, no network).
- [ ] An extension resolver and curl's `--doh-url` together are refused with a message naming both (unit test), recorded in an ADR.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
