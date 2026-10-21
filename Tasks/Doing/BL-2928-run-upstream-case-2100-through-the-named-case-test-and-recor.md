---
id: BL-2928
title: Run upstream case 2100 through the named-case test and record whether the DoH POSTs now reach sws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.NamedCase.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2928 — Run upstream case 2100 through the named-case test and record whether the DoH POSTs now reach sws

## Goal

`CURL_UPSTREAM_CASE=2100` runs through the named-case test instead of ending `skipped: Curl lacks the feature Debug`, and the first difference (if any) is recorded.

## Context

BL-2919 routed `--doh-url` through the harness's dialer, but its criterion 3 could not be checked: the named-case test skips case 2100 for the `Debug` feature (`UpstreamCaseScreening.UnsupportedFeature`), which the ratchet's harness stands in for (ADR-0497). Find why the named-case run lacks that stand-in and give it the same one.

## Acceptance criteria

- [ ] `CURL_UPSTREAM_CASE=2100` is no longer skipped for `Debug`; the first difference, if any, is recorded in Notes and is no longer `Could not resolve host`.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 17acd8f2.
