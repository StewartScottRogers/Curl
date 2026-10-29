---
id: BL-4097
title: Add certificate verification hooks for extensions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Certificates, Curl.Extension.Abstractions.UnitTests/Certificates, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4097 — Add certificate verification hooks for extensions

## Goal

An extension activated by its option can add a check to a server certificate chain after curl's own verification passes, and fail the transfer with exit 60 when its check fails.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Needed by DANE/TLSA, Certificate Transparency and ML-DSA certificates. An extension check may only add a refusal, never accept a chain curl refused.

## Acceptance criteria

- [x] A test extension refuses one chain curl accepted, and the transfer exits 60 (unit test, no network).
- [x] An extension cannot turn a chain curl refused into an accepted one (unit test).
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Design (ADR-1016): `Certificates/ICertificateCheckHook` returns null or a refusal message; `CertificateCheckHooks` keeps every activated hook and wraps curl's `IConnector` and `ITlsProvider`, checking the `PeerCertificates` of each connect curl accepted and turning the first refusal into exit 60. A failed result (curl's refusal) is returned untouched without asking any hook, so no hook can accept it. The check sits on `ConnectResult`, so `Curl.Tls` and `Curl.Tls.UnitTests` needed no change. Engine: `extensionCertificateConnector` (after `extensionConnector`) and `extensionTlsProvider`.
- Sensible defaults taken: hooks compose (all run, first refusal wins) rather than refusing two options; reused connections and connections without TLS are not checked; a TLS provider upgrade is checked even when it reports no certificate; the check also runs under `-k`.
- Touches widened with `Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs` (the engine seam's test, where the BL-4096 resolver seam is tested) and `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` (the library's document); no task in Doing on origin/work/dark-factory names either.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.Abstractions.UnitLibrary,Curl.Engine.UnitLibrary`: both 100% line and branch, 0 failing members. Full build clean, fast tests green on Windows; the tests use no path, OS error or certificate and wait on nothing concurrent, so they are platform-neutral for CI on Linux and macOS.
- Filed BL-4202 for the Curl.Console wiring.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 7348d087 76b0814b e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. An activated extension can add a certificate check that refuses a chain curl accepted with exit 60, and never accept one curl refused
