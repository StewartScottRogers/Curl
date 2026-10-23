---
id: BL-3261
title: Port the tester's cases.py to a CLI HTTP differential case file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3259]
touches: [Curl.Conformance.Differential.UnitLibrary/Cases/CliHttp/CliHttpCases.json, Curl.Conformance.Differential.UnitTests/Cases/CliHttp/CliHttpCasesTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3261 — Port the tester's cases.py to a CLI HTTP differential case file

## Goal

`Curl.Conformance.Differential.UnitLibrary/Cases/CliHttp/CliHttpCases.json` holds every case of the tester's cases.py (380 case lines, loops expanded) as data, and a unit test reads it with `DifferentialCaseFileReader`.

## Context

The tester's files are in `Z:\repos\Curl.difftest\2026-10-22\` (reference only: never run or commit Python; a PowerShell or C# file-based converter is fine, or port by hand). Each `case(name, cmd, stdin, files, timeout, skip_wire, cmp_err, ctl)` becomes one JSON case: `name`, `command` (with `{U}` and `{CWD}` kept), `stdin`, `files`, `timeoutSeconds`, `compareWire` = not skip_wire, `compareStderr`; byte content uses BL-3259's properties. Names built in loops with `re.sub(r"\W", "_", ...)` are expanded literally. Tag a case that only shows a reference-build fault with `limitations` (ADR-0782). Split out of BL-3233, which runs the file against both binaries.

## Acceptance criteria

- [x] `Cases/CliHttp/CliHttpCases.json` holds one case per case the source file produces, with the same names; a unit test reads it and pins the case count and one case's arguments.
- [x] Build clean with -warnaserror and fast tests green.

## Notes

- Filed by BL-3233's lane-3 run on 2026-10-23 when sizing it.
- Ported with a throwaway PowerShell converter (outside the repository) that reads each `case(...)` line's Python literals, takes the `else` side of every `X if False else Y`, drops line 329 (`... if False else None` never calls `case`), and expands the three loops (redirect codes, statuses, the 57 write-out variables, names through `\W` -> `_`). 454 cases: 379 top-level calls, 10 + 8 + 57 from loops.
- Only non-default properties are written (`timeoutSeconds` != 20, `compareStderr: false` for `cmp_err=False`, `compareWire: false` for `skip_wire=True`). `b"x" * 2_000_000` is `{"repeat": "x", "count": 2000000}`; `bytes(range(65, 91)) * 4` is plain text (A-Z four times).
- Decision: the runner substitutes `{U}` only in arguments, so the two cases whose file or stdin holds the Python `U` (`config-file-url`, `config-file-stdin`) carry the literal `http://127.0.0.1:18080` the tester's runner used (PORT 18080).
- Decision: `http2-prior` and `http2-plain-upgrade` are tagged `NoHttp2` (the reference build has no HTTP/2); no other case only shows a reference-build fault (no non-ASCII argument, no post-2038 date, no IDN host).
- The source repeats two names, `w-stderr` and `w-onerror` (each made by the write-out loop and again by an explicit case); kept as in the source because the criterion asks for the same names, and pinned by a test. BL-3233's runner must not key working directories on the name alone for these two (the tester's runner overwrote the first with the second).
- No production code changed (data file only, an embedded resource already globbed by the csproj), so Measure-CodeQuality was not needed. Tests: Curl.Conformance.Differential.UnitTests 224 passed; solution build -warnaserror 0 warnings; solution fast tests no failures. The test reads an embedded resource only, so it is platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: e32b2b1c 566e31a4.
- 2026-10-23: Doing -> Done. CliHttp/CliHttpCases.json holds all 454 cases of the tester's cases.py, read by DifferentialCaseFileReader in a unit test
