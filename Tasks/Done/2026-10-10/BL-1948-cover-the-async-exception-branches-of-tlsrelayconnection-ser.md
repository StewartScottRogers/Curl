---
id: BL-1948
title: Cover the async exception branches of TlsRelayConnection.ServeAsync and TlsServerConnection.ServeAsync
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1914]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-10
---
# BL-1948 — Cover the async exception branches of TlsRelayConnection.ServeAsync and TlsServerConnection.ServeAsync

## Goal

`TlsRelayConnection.ServeAsync` and `TlsServerConnection.ServeAsync` in Curl.Conformance.UnitLibrary reach 100% branch coverage.

## Context

Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary (2026-10-09, BL-1914) reports both at 100% lines but 62.5% and 25% branches: the untaken branches are the compiler's for `await using` and an async try/catch/finally (a null `SslStream`, exception dispatch). Replacing `await using` with explicit try/finally made it worse (50%). Find a shape - for example a non-async wrapper over a helper, or a test that faults each await - that covers them without suppressing coverage.

## Acceptance criteria

- [x] Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary reports no failing member in TlsRelayConnection.cs or TlsServerConnection.cs.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Measured 2026-10-10 (Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary): 0 failing members; both ServeAsync methods already pass after the catch-then-cleanup shape in the current source. No code change needed.

## Log

- 2026-10-09: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Already met: Measure-CodeQuality reports 0 failing members for Curl.Conformance.UnitLibrary, TlsRelayConnection and TlsServerConnection included
