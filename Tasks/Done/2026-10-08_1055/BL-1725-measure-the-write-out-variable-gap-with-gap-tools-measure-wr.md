---
id: BL-1725
title: Measure the --write-out variable gap with Gap/Tools/Measure-WriteOutGap.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1721, BL-1722]
touches: [Gap/Tools/Measure-WriteOutGap.ps1, Gap/Tools/Fixtures/writeout, Gap/Upstream/8.21.0/writeout.json, Gap/Instructions/Gap-Format.md]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1725 — Measure the --write-out variable gap with Gap/Tools/Measure-WriteOutGap.ps1

## Goal

`Gap/Tools/Measure-WriteOutGap.ps1` builds the `writeout` upstream inventory from the
release's `docs/cmdline-opts/write-out.md`. It then measures, for every variable, whether
`Curl.Console` recognises it and gives the same value as the matched reference curl.

## Context

This is ADR-0433 decision 2, area `writeout`. Formats are in
`Gap/Instructions/Gap-Format.md` (BL-1720). Use `Get-UpstreamRelease.ps1` (BL-1721) and
`Invoke-GapProbe.ps1` (BL-1722).

**Inventory.** Read `docs/cmdline-opts/write-out.md` in the release and find how it lists
variables. Take the pattern from the file, not from memory. Record each variable's name and,
where the text says which version added it, `introducedIn`. The function-style forms the
document describes (such as `%header{name}`, `%output{file}` and `%time{format}`) are items
too, keyed by their form name (`writeout:%header{}`). Key: `writeout:<name>`.

**Facets and probing.** For each variable, run
`-s -o <null device> -w "%{<name>}" <file URL of a small temporary file>` through both
binaries with `Invoke-GapProbe`. On Windows the file URL needs the drive letter
(`file:///C:/...`). This is a tool run on the machine, not a cross-platform test, so build
the URL for the platform the tool runs on.

- `writeout:<name>`: `match` when both binaries agree on whether the variable is unknown,
  that is, on whether stderr carries the reference's unknown-variable warning. Measure the
  warning's text on the reference curl first and pin it in a fixture. Never write it from
  memory.
- `writeout:<name>:value`: `match` when stdout is byte-equal. Skip this facet for volatile
  variables, whose value differs between any two runs (times, speeds, local port and the
  like). Decide that list from `write-out.md`'s own descriptions, keep it in the fixture,
  and name it in the header help.

Without a matching reference, the recognition facet falls back to the documents: every
documented variable is expected to be recognised. The value facet becomes `unmeasured` with
reason `no-reference`.

**Parameters.** `-UpstreamRoot`, `-Version`, `-Candidate`, `-OutFile`, `-InventoryOnly`
(no probing and no Windows-only API; BL-1735 runs it on Linux), `-SelfTest` (a fake
`write-out.md` under `Gap/Tools/Fixtures/writeout/` and canned probe answers through
`-ProbeResults`).

Commit `Gap/Upstream/8.21.0/writeout.json`.

## Acceptance criteria

- [x] `Gap/Tools/Measure-WriteOutGap.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks: plain and function-style variables are both inventoried; agreement on recognition gives `match`; Curl reporting a variable unknown that the reference knows gives `gap`; a volatile variable has no value facet; differing stdout on a non-volatile variable gives a value `gap`; with no reference the value facet is `unmeasured` with `no-reference`.
- [x] `Gap/Upstream/8.21.0/writeout.json` is committed and valid against `Gap-Format.md`.
- [x] A real run on Windows writes the measurement, and its counts are recorded in this task's Notes.
- [x] The header help documents every parameter and the volatile list. The script is ASCII only.

## Notes

- Inventory pattern, read from 8.21.0's write-out.md: variables are "## `<name>`" headings
  after "The variables available are:", and the list ends at a bare "##"; the "## `%a`"
  headings after it are strftime conversions for %time{}, not variables. A heading with
  braces (`header{name}`, `output{filename}`, `time{format}`) is a function-style form,
  keyed `writeout:%header{}`. introducedIn is the section's first "(Added in X)".
  8.21.0 has 76 items: 73 plain variables and 3 function forms.
- Unknown-variable warning measured on curl 8.21.0 (Git for Windows, Schannel):
  `curl: unknown --write-out variable: 'zzz_nonexistent'`, pinned in
  `Fixtures/writeout/reference-unknown-variable.txt`. Recognition looks for its prefix
  up to the quote, so it also catches function forms.
- Probe formats for function forms (a default choice): `%header{content-type}`,
  `%output{<scratch>/output.txt}`, `%time{%Y}`, else `%<form>{x}`. The file URL is
  percent-encoded (`System.Uri.AbsoluteUri`): an unencoded space in "Stewart Rogers" made
  the reference exit 3.
- Volatile list (no value facet), from write-out.md's descriptions: %time{}, json (holds
  times and speeds), local_port, speed_download, speed_upload and the nine time_*
  variables. Counters such as conn_id, xfer_id and num_connects are deterministic per run
  and keep their value facet.
- stdout is decoded as ISO-8859-1 so string equality is byte equality; non-printable
  bytes are shown as \xNN in expected/actual.
- Added `Gap/Instructions/Gap-Format.md` to touches: the `no-reference` reason the task
  specifies was not in the writeout reason vocabulary, and Gap-Format says a tool adds a
  new reason there first. No task in Doing on origin/work/dark-factory names that file.
- Real run on Windows, 2026-10-08, candidate Curl.Console Release, reference curl 8.21.0
  Schannel: match 138, gap 0, unmeasured 0, excluded 0 (x 138, y 138) - 76 recognition
  facets and 62 value facets.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Measure-WriteOutGap.ps1 inventories 76 write-out variables of curl 8.21.0 and measures recognition and value against the reference: 138 of 138 facets match on Windows
