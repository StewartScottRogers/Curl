---
id: BL-1791
title: Normalise random multipart boundaries before Measure-ReferenceCrossCheck.ps1 compares request bytes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Gap/Tools/Measure-ReferenceCrossCheck.ps1, Gap/Tools/Fixtures/crosscheck]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1791 — Normalise random multipart boundaries before Measure-ReferenceCrossCheck.ps1 compares request bytes

## Goal

A multipart form case (upstream test9, test39, test44) no longer counts as a disagreement between the reference curl and Curl.Console only because each run picks a random boundary.

## Context

BL-1730's real run on 8.21.0 cases 1-80 reported test9, test39 and test44 as "request differs at byte 17x": the bytes differ inside the random multipart boundary (------------------------<random>). Upstream's own <strip> rules replace the boundary for the same reason. Replace each binary's boundary, read from its Content-Type: multipart/...; boundary= header, with a fixed token in both request and stdout before Compare-Recording.

## Acceptance criteria

- [x] `Gap/Tools/Measure-ReferenceCrossCheck.ps1 -SelfTest` has a PASS line for two recordings that differ only in their multipart boundary agreeing, under Windows PowerShell 5.1 and PowerShell 7.
- [x] A real run on 8.21.0 cases 1-80 no longer lists test9, test39 or test44 as disagreements for a boundary difference.

## Notes

- `Read-Recording` now passes request and stdout through `Set-FixedMultipartBoundary`: the
  boundary is read from the recording's own request (`Content-Type: multipart/...;
  boundary=`, quoted or not) and every occurrence is replaced by `<multipart-boundary>`.
  Each binary's boundary comes from its own request, so the two need not share a length.
  Decision (sensible default): a request with no multipart Content-Type is left as it is.
- Self-test fixture case 9: two `--form` recordings whose requests differ only in the
  boundary; counts in the existing checks moved by one (crossChecked 6, match 5, limit 6).
  11 PASS, 0 FAIL under Windows PowerShell 5.1 and PowerShell 7.
- Real run, Windows, reference curl 8.21.0 Schannel, in-process measurement of 8.21.0
  cases 1-80 (match 58, gap 14, unmeasured 8), 258 s: cross-checked 47; left out by
  not-match-or-gap 8, server-not-http-alone 2, reply-not-one-data 23; reference-diverges 4
  (test17, test56, test71, test73); disagreements 2 (test60, test62). test9, test39 and
  test44 stay match. test71 (BL-1730 counted it as a disagreement) also differed only in
  its boundary and now joins reference-diverges.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Measure-ReferenceCrossCheck.ps1 replaces each run's random multipart boundary before comparing, so test9/39/44 agree
