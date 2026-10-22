---
id: BL-3015
title: Run upstream cases 1652, 2413, 3219 and 3302 over their ported unit programs and put the passing ones on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3014]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
model: sonnet
requirement: none
created: 2026-10-21
completed:
---
# BL-3015 — Run upstream cases 1652, 2413, 3219 and 3302 over their ported unit programs and put the passing ones on the ratchet

## Goal

Upstream cases 1652, 2413, 3219 and 3302 run over their ported unit programs and every one that passes is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2925, after BL-3014 commits the ports. For each case run the upstream conformance test with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows (see how the other ported unit-program cases are run in `Curl.Conformance.UnitTests`, and the header of `PassingUpstreamCases.txt`) and add one line per passing case. A case that does not pass: record its first difference in Notes and file a new task for the product fix rather than widening this one; a case with a C-only reason gets that reason recorded in the ADR from BL-2840.

## Acceptance criteria

- [ ] Cases 1652, 2413, 3219 and 3302 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows, or Notes name the first difference and the follow-up task for each that does not.
- [ ] Each passing case has one line on `PassingUpstreamCases.txt` and the ratchet test passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Conformance.UnitTests`.

## Notes

## Log

- 2026-10-21: Created.
