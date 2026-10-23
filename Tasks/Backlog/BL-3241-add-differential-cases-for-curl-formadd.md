---
id: BL-3241
title: Add differential cases for curl_formadd
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3239]
touches: [Curl.Conformance.Differential.UnitLibrary/Libcurl/Formadd, Curl.Conformance.Differential.UnitTests/Libcurl/Formadd, Curl.Conformance.Differential.IntegrationTests/LibcurlFormadd]
requirement: none
created: 2026-10-22
completed:
---
# BL-3241 — Add differential cases for curl_formadd

## Goal

New differential cases exercise `curl_formadd`, `curl_formget` and `curl_formfree` (every CURLFORM_* option, its error codes, and the posted multipart body) against Curl's Libcurl and libcurl 8.22.0.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. The tester did not cover curl_formadd. Builds on BL-3239's MIME comparison.

## Acceptance criteria

- [ ] formadd cases covering every CURLFORM_* option and each CURL_FORMADD_* error replay against both libraries; one run's report is recorded under Notes, each difference filed.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
