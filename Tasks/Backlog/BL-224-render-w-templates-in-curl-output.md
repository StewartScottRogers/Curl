---
id: BL-224
title: Render -w templates in Curl.Output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-224 — Render -w templates in Curl.Output

## Goal

A `-w` template renderer in `Curl.Output.UnitLibrary` handles variables, headers, output redirection and escapes over a variable source.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item O1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- https://curl.se/docs/manpage.html#-w (curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `%{var}`, `%header{name}`, `%output{file}`, `%output{>>file}`, `%{stdout}`, `%{stderr}`, `\n`, `\r`, `\t`, `\\` and `%%` render as curl does.
- [ ] An unknown variable gives the measured warning line.
- [ ] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Plan item: O1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Stewart: lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-224-lane-1-20260926-132927.
- 2026-09-26: Blocked -> Backlog. Not for Stewart: integration failed (fast tests red after rebase). The work is on branch factory/BL-224-lane-1-20260926-132927; start with git cherry-pick --no-commit factory/BL-224-lane-1-20260926-132927 and fix it.
