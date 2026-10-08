---
id: BL-1714
title: Cover the last branch of Curl.Tls Tls13ClientHelloBuilder.BuildOptionalExtension
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1714 — Cover the last branch of Curl.Tls Tls13ClientHelloBuilder.BuildOptionalExtension

## Goal

`Tls13ClientHelloBuilder.BuildOptionalExtension` reaches 100% branch coverage, so Curl.Tls.UnitLibrary has no failing member in `Measure-CodeQuality.ps1`.

## Context

- Found by BL-1712's measurement (2026-10-07): `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports Curl.Tls.UnitLibrary at 100% line, 99.95% branch, one failing member: `Tls13ClientHelloBuilder.BuildOptionalExtension(TlsExtensionType)` at `Tls13ClientHelloBuilder.cs:129`, branch 92.86%, Cx 14, failing on "branch complexity".
- The merged Cobertura reports line 129 (the `type switch` expression) at 9/10 conditions. BL-1712 did not change this file; the gap predates it.
- Start by finding which arm or comparison no test reaches (ServerName null, ALPN empty, StatusRequest, PostHandshakeAuth, or the `_` arm), and add a Curl.Tls.UnitTests test for it; if the 10th branch is a compiler artifact of the switch, reshape the method so every branch is a reachable one.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports no failing member in `Tls13ClientHelloBuilder`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
