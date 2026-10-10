---
id: BL-1940
title: Bring SocksServerConnection and SwsHttpServerConnection under the gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1940 — Bring SocksServerConnection and SwsHttpServerConnection under the gates

## Goal

Every failing member of SocksServerConnection (Socks5 request, Socks4, AnswerAsync) and SwsHttpServerConnection.ReadWithNothingSentAsync is under 100% branch coverage and complexity of at most 10.

## Context

Split from BL-1936 (BL-1929's measurement, 2026-10-09). Split methods (a lookup table of commands, say) and add the missing branch tests; never raise a threshold. Measure once with `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary -ReportPath <file>` and read the report.

## Acceptance criteria

- [x] The members named in the Goal are absent from the failing list of `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

Measured: all four failed on complexity only (coverage already 100%). Split out FindSocks4End, Socks5RequestEnd, Socks5AddressLength, AnswerSocks5Async and ReadWithNothingOwedAsync; behaviour unchanged.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Complexity split, gates green
