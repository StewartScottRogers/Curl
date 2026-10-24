---
id: BL-3414
title: Interpret upstream test1275.pl in C#: capital letters after a period in the vendored Markdown docs (case 1275)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3407]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1275Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1275.cs, Curl.Conformance.UnitTests/UpstreamTest1275ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3414 — Interpret upstream test1275.pl in C#: capital letters after a period in the vendored Markdown docs (case 1275)

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- Interpreted test1275.pl (curl 8.22.0, fetched from the tag) in UpstreamTest1275Script: every vendored *.md except the root README.md, in ordinal order; prints ok on success, else the script's stderr lines and exit 1. Case 1275 added to PassingUpstreamCases.txt. Fast tests green (4312 passed), Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Done. case 1275 (test1275.pl, capital letters after a period in Markdown docs) interpreted in C# and on the ratchet
