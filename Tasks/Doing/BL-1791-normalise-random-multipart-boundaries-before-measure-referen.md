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
completed:
---
# BL-1791 — Normalise random multipart boundaries before Measure-ReferenceCrossCheck.ps1 compares request bytes

## Goal

A multipart form case (upstream test9, test39, test44) no longer counts as a disagreement between the reference curl and Curl.Console only because each run picks a random boundary.

## Context

BL-1730's real run on 8.21.0 cases 1-80 reported test9, test39 and test44 as "request differs at byte 17x": the bytes differ inside the random multipart boundary (------------------------<random>). Upstream's own <strip> rules replace the boundary for the same reason. Replace each binary's boundary, read from its Content-Type: multipart/...; boundary= header, with a fixed token in both request and stdout before Compare-Recording.

## Acceptance criteria

- [ ] `Gap/Tools/Measure-ReferenceCrossCheck.ps1 -SelfTest` has a PASS line for two recordings that differ only in their multipart boundary agreeing, under Windows PowerShell 5.1 and PowerShell 7.
- [ ] A real run on 8.21.0 cases 1-80 no longer lists test9, test39 or test44 as disagreements for a boundary difference.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
