---
id: BL-1814
title: Close GF-0021: --variable ... {{name:json}} encodes a Unicode string differently (stderr)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1814 — Close GF-0021: --variable ... {{name:json}} encodes a Unicode string differently (stderr)

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0021 (--variable ... {{name:json}} encodes a Unicode string differently (stderr)), so a later gap analysis measures each of `behaviour:test268` as `match`.

## Context

- Finding: GF-0021, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Medium. Introduced in: not stated upstream.
- Items: `behaviour:test268`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test268 expected 'upstream test268 passes' (the reference curl exits 0), actual 'stderr differs' from the reference curl. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 268

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's --variable / --expand-* expansion, make the ':json' function escape a Unicode string byte for byte as curl 8.21.0 does: control characters as \uXXXX, and bytes >= 0x80 passed through. Make the stderr match the reference for upstream tests/data/test268.

## Acceptance criteria

- [x] `behaviour:test268`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause: the `json` function already matched curl (bytes >= 0x80 pass through; the request body was
  E2 80 9C from both). The stderr difference was curl's leading-Unicode warning: `getparameter` checks the
  value after `--expand-` expansion, so the Windows Schannel build also prints
  `Warning: The argument '“' starts with a Unicode character. Maybe ASCII was intended?` when a
  variable's file bytes lead the value. Curl checked only when arguments are read as UTF-8 or come from `-K`.
- Fix: `CommandLineOptions.ApplyingValueLedByVariableBytes`, set while an `--expand-` value whose template
  opens with a replaced reference is applied; the leading-Unicode check honours it.
- Measured 2026-10-08 with Record-CurlExchange.ps1, real curl 8.21.0 (Windows) against Curl on test268's
  command line: warning lines and request bytes now match.
- Choice (sensible default): a template led by typed text never warns on Windows, since curl sees ANSI
  bytes there. A variable given inline (`--variable a=“`) and expanded at the start still warns in Curl on
  Windows, where curl would see ANSI bytes; no upstream case measures it, so it is left as is.
- Pinned by three new tests in `CommandLineLeadingUnicodeWarningTests`. No option added or changed, so
  `--ai-help` needs nothing.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Expanded values led by variable bytes now get curl's leading-Unicode warning on Windows; test268 stderr matches real curl
