---
id: BL-1726
title: Measure the exit code and error message gap with Gap/Tools/Measure-ExitCodeGap.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1721]
touches: [Gap/Tools/Measure-ExitCodeGap.ps1, Gap/Tools/Fixtures/exitcodes, Gap/Upstream/8.21.0/exitcodes.json]
requirement: none
created: 2026-10-08
completed:
---
# BL-1726 — Measure the exit code and error message gap with Gap/Tools/Measure-ExitCodeGap.ps1

## Goal

`Gap/Tools/Measure-ExitCodeGap.ps1` builds the `exitcodes` upstream inventory from the
release's libcurl error table, exit-code man page section and `lib/strerror.c`. It then
measures whether Curl has every code under the same number with the same
`curl_easy_strerror` text.

## Context

This is ADR-0433 decision 2, area `exitcodes`. Formats are in
`Gap/Instructions/Gap-Format.md` (BL-1720). Use `Get-UpstreamRelease.ps1` (BL-1721), which
extracts `lib/strerror.c` for this area alone.

**Upstream sources, all at the targeted release.**

- `docs/libcurl/libcurl-errors.md`: each `CURLE_*` name with its number.
- `docs/cmdline-opts/_EXITCODES.md`: the curl tool's exit codes and their man-page text.
- `lib/strerror.c`: `curl_easy_strerror`'s text for each `CURLE_*`, read as text, never
  compiled (ADR-0433 decision 1).

Read each file's structure in the release and parse what is there. Do not assume a layout.
Each code gives up to three keys: `exitcodes:<n>` (the code exists under that number),
`exitcodes:<n>:strerror` (the `curl_easy_strerror` text) and `exitcodes:<n>:man`
(present in `_EXITCODES.md`; this is inventory only and carries no candidate facet).
Codes that libcurl marks obsolete or unused stay in the inventory with an attribute saying
so. Decide in the header help how they are scored: as `excluded` with reason `obsolete`
when upstream itself never returns them.

**Curl's side.** It is read from source text, because the table is internal to
`Curl.Console`:

- `Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs`: the `CurlExitCode` enum members
  and their numbers.
- `Curl.Console/CurlEasyErrorText.cs`: the `[CurlExitCode.X] = "text"` entries and its
  `UnknownError` constant.

`exitcodes:<n>` is `match` when an enum member has that number. `exitcodes:<n>:strerror` is
`match` when Curl's text for that member equals `lib/strerror.c`'s byte for byte. The
measurement's `reference` is `null`, because this area compares with the release's text,
not a binary. Record the two Curl source files and their commit in `evidence`.

**Parameters.** `-UpstreamRoot`, `-Version`, `-RepositoryRoot` (default the repository the
script is in), `-OutFile`, `-InventoryOnly` (no Windows-only API; BL-1735 runs it on
Linux), `-SelfTest` (fake `libcurl-errors.md`, `_EXITCODES.md` and `strerror.c`, plus fake
copies of the two Curl source files, under `Gap/Tools/Fixtures/exitcodes/`).

Commit `Gap/Upstream/8.21.0/exitcodes.json`.

## Acceptance criteria

- [ ] `Gap/Tools/Measure-ExitCodeGap.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks: numbers and names parse from the error table; texts parse from `strerror.c`, including a text split across C string literals; a missing enum member gives `gap`; an equal text gives `match` and a one-character difference gives `gap`; an obsolete code is scored as the header help says.
- [ ] `Gap/Upstream/8.21.0/exitcodes.json` is committed and valid against `Gap-Format.md`.
- [ ] A real run against this repository writes the measurement, and its counts are recorded in this task's Notes.
- [ ] The header help documents every parameter, every source file and the scoring rules. The script is ASCII only.

## Notes

## Log

- 2026-10-08: Created.
