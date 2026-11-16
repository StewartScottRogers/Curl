---
id: BL-5271
title: Fix AF-0817: Surviving mutant: an ODoH response whose status line has no reason phrase can be refused, failing the resolve
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5271 — Fix AF-0817: Surviving mutant: an ODoH response whose status line has no reason phrase can be refused, failing the resolve

## Goal

The defect the audit office reported as AF-0817 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0817 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:32`

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs:32`

`parts.Length >= 2` changed to `parts.Length > 2` survived. 'HTTP/1.1 200' (no reason phrase, valid under RFC 9112) then parses to no status, so the response is null, the resolver gets no addresses and the transfer ends with a resolve failure instead of connecting. Every test response carries a reason phrase.

Reproduction: the audit office's mutation test changed `Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs line 32` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extension.ObliviousDoh.UnitLibrary/Http/ObliviousDohHttpResponseParser.cs line 32`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `ObliviousDohHttpTests.Parse_FramesTheBodyAsRfc9112Section6_3` with the new row `HTTP/1.1 200` (no reason phrase); with the mutant it failed, reverted it passes (97 of 97). Production code unchanged. Fast tests of the library pass; the full-solution run is left to the shift (test-only change).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 5bda924b 9742157d.
