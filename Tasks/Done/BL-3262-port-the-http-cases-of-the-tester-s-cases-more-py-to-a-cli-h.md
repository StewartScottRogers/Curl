---
id: BL-3262
title: Port the HTTP cases of the tester's cases_more.py to a CLI HTTP differential case file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3259]
touches: [Curl.Conformance.Differential.UnitLibrary/Cases/CliHttp/CliHttpMoreCases.json, Curl.Conformance.Differential.UnitTests/Cases/CliHttp/CliHttpMoreCasesTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3262 — Port the HTTP cases of the tester's cases_more.py to a CLI HTTP differential case file

## Goal

`Curl.Conformance.Differential.UnitLibrary/Cases/CliHttp/CliHttpMoreCases.json` holds every case of the tester's cases_more.py (228 case lines, for-loops expanded; leave out cases that need a non-HTTP or TLS server, which BL-3234 and BL-3235 port, and list them under Notes) as data, and a unit test reads it with `DifferentialCaseFileReader`.

## Context

The tester's files are in `Z:\repos\Curl.difftest\2026-10-22\` (reference only: never run or commit Python; a PowerShell or C# file-based converter is fine, or port by hand). Each `case(name, cmd, stdin, files, timeout, skip_wire, cmp_err, ctl)` becomes one JSON case: `name`, `command` (with `{U}` and `{CWD}` kept), `stdin`, `files`, `timeoutSeconds`, `compareWire` = not skip_wire, `compareStderr`; byte content uses BL-3259's properties. Names built in loops with `re.sub(r"\W", "_", ...)` are expanded literally. Tag a case that only shows a reference-build fault with `limitations` (ADR-0782). Split out of BL-3233, which runs the file against both binaries.

## Acceptance criteria

- [x] `Cases/CliHttp/CliHttpMoreCases.json` holds one case per case the source file produces, with the same names; a unit test reads it and pins the case count and one case's arguments.
- [x] Build clean with -warnaserror and fast tests green.

## Notes

- Filed by BL-3233's lane-3 run on 2026-10-23 when sizing it.
- Ported with a throwaway C# file-based converter (outside the repository, not committed): the 228 case lines expand to 431 cases (the for-loops expanded; `opt-version-short` evaluates to None in the source and adds no case).
- No case was left out: none of cases_more.py needs a non-HTTP or TLS server (`ftp://127.0.0.1:1/` and `ftp.nonexistent.invalid` only fail to connect or resolve, as the source intends).
- Decision: the source names the URL-parsing cases `url-<sanitised[:45]><abs(hash(u)) % 997>`, and Python seeds `hash()` per process, so that suffix differs on every run and cannot be reproduced. The suffix here is the URL's 1-based position in the source list, which keeps every name unique and stable. Every other name matches the source exactly.
- File contents the source built with f-strings hold the literal `http://127.0.0.1:18080`, as the source wrote them; only `command` keeps `{U}` and `{CWD}`. Defaults (timeout 20, compareStderr true, compareWire true) are left out of the JSON; no case uses skip_wire. No case is tagged with `limitations`: tagging needs the run against both binaries (BL-3233).
- Build clean with -warnaserror and fast tests green on Windows; the case file is data and the test reads it without touching the platform.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ba50fbb9 566e31a4.
- 2026-10-23: Doing -> Done. CliHttpMoreCases.json holds cases_more.py's 431 HTTP cases, read and pinned by a unit test
