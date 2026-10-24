---
id: BL-3408
title: Interpret upstream test1140.pl in C#: nroff syntax of the cd2nroff-generated man pages (case 1140)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1140Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1140.cs, Curl.Conformance.UnitTests/UpstreamTest1140ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3408 — Interpret upstream test1140.pl in C#: nroff syntax of the cd2nroff-generated man pages (case 1140)

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- Case 1140 passes (on PassingUpstreamCases.txt) via UpstreamTest1140Script, a port of tests/test1140.pl: the libcurl .3 pages come from UpstreamCd2nroff (ADR-0752); the .1 pages (managen output) match nothing in a release tree and scan nothing. Focused tests in UpstreamTest1140ScriptTests; Measure-CodeQuality for Curl.Conformance.UnitLibrary reports 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Done. case 1140 passes: test1140.pl interpreted in C# (nroff \f syntax and man page references)
