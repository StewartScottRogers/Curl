---
id: BL-4097
title: Add certificate verification hooks for extensions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Certificates, Curl.Extension.Abstractions.UnitTests/Certificates, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4097 — Add certificate verification hooks for extensions

## Goal

An extension activated by its option can add a check to a server certificate chain after curl's own verification passes, and fail the transfer with exit 60 when its check fails.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Needed by DANE/TLSA, Certificate Transparency and ML-DSA certificates. An extension check may only add a refusal, never accept a chain curl refused.

## Acceptance criteria

- [ ] A test extension refuses one chain curl accepted, and the transfer exits 60 (unit test, no network).
- [ ] An extension cannot turn a chain curl refused into an accepted one (unit test).
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
