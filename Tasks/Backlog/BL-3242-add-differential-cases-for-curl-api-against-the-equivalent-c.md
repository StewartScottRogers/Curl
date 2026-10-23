---
id: BL-3242
title: Add differential cases for Curl.Api against the equivalent curl command lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Api, Curl.Conformance.Differential.UnitTests/Api, Curl.Conformance.Differential.IntegrationTests/Api]
requirement: none
created: 2026-10-22
completed:
---
# BL-3242 — Add differential cases for Curl.Api against the equivalent curl command lines

## Goal

New differential cases run `Curl.Api` requests beside the equivalent curl 8.22.0 command lines on BL-3232's loopback routes and report where the wire bytes or results differ.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. The tester did not cover Curl.Api. Reuse BL-3233's CLI HTTP cases where a Curl.Api call expresses the same transfer.

## Acceptance criteria

- [ ] Curl.Api cases for the common request shapes (GET, POST forms, headers, redirects, auth, cookies) replay beside curl; one run's report is recorded under Notes, each difference filed.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
