---
id: BL-2136
title: Fix CI failure test1462 on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2136 — Fix CI failure test1462 on Linux and macOS and Windows

## Goal

`test1462` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1462` failed on Linux and macOS and Windows in CI run 38431484503 (https://github.com/StewartScottRogers/Curl/actions/runs/38431484503). First failing commit: 48919fca.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38431484503 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test1462` passes locally, is back on `PassingUpstreamCases.txt`, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Cause (lane 1, BL-2134 integration): BL-2126 re-vendored upstream test data at curl-8_22_0, whose `--help category` lists a new `mqtt  MQTT protocol` row. Curl.Cli still prints curl 8.21.0's help pages. test1462 was taken off `PassingUpstreamCases.txt` so the combined tree is green; put it back once fixed. Fix: add `CurlHelpCategories.Mqtt` and its row in `CurlHelpText.Categories`, and re-measure every `Curl.Cli.UnitTests/HelpReference` fixture against curl 8.22.0 - the usage page also lists `mqtt` and `-d, --data` now reads "Post data", so diff all of them, not only the category list. Keep `--ai-help` right.
