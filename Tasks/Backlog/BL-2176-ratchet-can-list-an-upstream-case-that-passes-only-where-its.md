---
id: BL-2176
title: Ratchet can list an upstream case that passes only where its features exist (Schannel test2087, test3023)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2176 — Ratchet can list an upstream case that passes only where its features exist (Schannel test2087, test3023)

## Goal

A case that needs a feature only one platform's Curl has (such as `Schannel`) can be on `PassingUpstreamCases.txt` without failing the ratchet on the other platforms, and upstream test2087 and test3023 are listed.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13 on Windows: test2087 and test3023 (`<features>` include `Schannel`) pass, but `UpstreamCaseRatchet` fails a listed case that is skipped (`Curl.Conformance.UnitLibrary/UpstreamCaseRatchet.cs`, line 42), and on Linux and macOS Curl has no `Schannel`, so listing them would turn CI red there. Decide the mechanism (for example, a listed case skipped only for a feature this platform's matched curl lacks is not a regression), record it in an ADR, and pin it in `UpstreamCaseRatchetTests`.

## Acceptance criteria

- [ ] test2087 and test3023 are on `PassingUpstreamCases.txt`, and the fast tests pass on Windows, Linux and macOS in CI.
- [ ] A listed case that is skipped for any other reason still fails the ratchet (unit test).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-13: Created.
