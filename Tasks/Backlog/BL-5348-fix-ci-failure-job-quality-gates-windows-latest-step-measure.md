---
id: BL-5348
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/McpClient/Stdio/ClosableObservedStream.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5348 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 44041873836 (https://github.com/StewartScottRogers/Curl/actions/runs/44041873836). First failing commit: 3f0cbe7b.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; ClosableObservedStream.Read(byte[], int, int) is outside a gate: line; ClosableObservedStream.Flush() is outside a gate: line; ClosableObservedStream.Seek(long, System.IO.SeekOrigin) is outside a gate: line; ClosableObservedStream.Write(byte[], int, int) is outside a gate: line

Lanes test only on Windows, so reproduce with `gh run view 44041873836 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-17: Created.
