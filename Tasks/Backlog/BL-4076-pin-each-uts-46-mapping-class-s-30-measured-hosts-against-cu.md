---
id: BL-4076
title: Pin each UTS 46 mapping class's 30 measured hosts against Curl's Host header
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4033]
touches: [Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr019Tests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4076 — Pin each UTS 46 mapping class's 30 measured hosts against Curl's Host header

## Goal

For every mapping class of BL-3829, at least 30 hosts measured against curl with libidn2 2.3.8 are pinned as Curl's `Host` header, so BL-3829's first criterion is met.

## Context

BL-3829 (ADR-0980) maps host names with the whole UTS #46 table and NFC. BL-4033 measured 443 hosts in eight classes (compatibility 68, East Asian 117, presentation forms 53, case 49, outside the BMP 42, Hangul 30, Indic nukta 38, combining-mark order 32) with curl 8.18.0 and libidn2 2.3.8 and pinned them as `TryNormalize_Measured*_ConvertsAsCurlWithLibidn2` rows in `CurlUrlHostNameMappingTests.cs`; its Notes hold the method. Check that every class has at least 30 pinned rows, that `Cgr019Tests.cs` agrees with the table, and that Curl's `Host` header (not only `TryNormalize`) equals the pinned host for a sample of each class. Add rows or tests for any class that falls short.

## Acceptance criteria

- [ ] Each of the eight classes above has at least 30 measured rows in `CurlUrlHostNameMappingTests.cs`, and a focused test per class asserts the `Host` header Curl builds equals the measured host.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3829 (its first criterion, measuring the hosts); the measurement itself is BL-4033's.
- BL-4033 used curl 8.18.0 as a stand-in for 8.22.0; the conversion is libidn2 2.3.8 in both.

## Log

- 2026-10-29: Created.
