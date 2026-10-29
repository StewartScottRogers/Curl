---
id: BL-4096
title: Add resolver hooks for extensions: alternative DNS transports
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Resolution, Curl.Extension.Abstractions.UnitTests/Resolution, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4096 — Add resolver hooks for extensions: alternative DNS transports

## Goal

An extension activated by its option can supply the `IDnsResolver` a transfer uses, e.g. DNS-over-TLS, DNS-over-QUIC or Oblivious DoH.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Needed by DoT/DoQ, Oblivious DoH and DNSSEC/DANE.

## Acceptance criteria

- [x] A test extension's resolver answers one fake transfer's lookup (engine unit test, no network).
- [x] An extension resolver and curl's `--doh-url` together are refused with a message naming both (unit test), recorded in an ADR.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: mirror BL-4095. `IResolverHook` (option + `ResolverOver(curlResolver)`) and `ResolverHooks.ActivatedBy(values, activators, dohUrlGiven)` in `Resolution/`; the engine takes `extensionResolver` on `CreateProtocolHandlers` and applies it to `dnsResolver` before any handler is built (ADR-1012).
- Decision: the `--doh-url` refusal lives in `ResolverHooks.ActivatedBy` (the engine never sees `--doh-url`), which takes whether it was given; `--doh-url` alone is unchanged. Refusing beats silently choosing one (ADR-1012).
- Engine test: an extension scheme handler looks its URL host up through `ProtocolHandlerServices.DnsResolver`, answered by the extension resolver; the connectors' own look-ups and the Console wiring are BL-4200.
- Added `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` to touches to document the hook (no other task in this checkout names it).
- Measured: Measure-CodeQuality for Curl.Extension.Abstractions.UnitLibrary and Curl.Engine.UnitLibrary, 0 failing members. Fast tests green (Engine 2925 passed, Extension.Abstractions 23 passed, no failures solution-wide).



## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a9629321 3acd8ed2 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. An activated extension's resolver hook replaces curl's resolver in the engine, and one beside --doh-url or another is refused naming both
