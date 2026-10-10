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
completed:
---
# BL-1948 — Cover the async exception branches of TlsRelayConnection.ServeAsync and TlsServerConnection.ServeAsync

## Goal

`TlsRelayConnection.ServeAsync` and `TlsServerConnection.ServeAsync` in Curl.Conformance.UnitLibrary reach 100% branch coverage.

## Context

Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary (2026-10-09, BL-1914) reports both at 100% lines but 62.5% and 25% branches: the untaken branches are the compiler's for `await using` and an async try/catch/finally (a null `SslStream`, exception dispatch). Replacing `await using` with explicit try/finally made it worse (50%). Find a shape - for example a non-async wrapper over a helper, or a test that faults each await - that covers them without suppressing coverage.

## Acceptance criteria

- [ ] Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary reports no failing member in TlsRelayConnection.cs or TlsServerConnection.cs.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-09: Created.
