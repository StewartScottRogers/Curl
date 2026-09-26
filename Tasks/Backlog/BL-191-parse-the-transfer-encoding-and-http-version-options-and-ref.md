---
id: BL-191
title: Parse the transfer-encoding and HTTP version options and refuse --http2 and --http3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-152]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-191 — Parse the transfer-encoding and HTTP version options and refuse --http2 and --http3

## Goal

`--compressed`, `--raw`, `-0`/`--http1.0`, `--http1.1`, `--tr-encoding`, `--ignore-content-length`, `--path-as-is` and `--request-target` parse, and `--http2`, `--http2-prior-knowledge` and `--http3` are refused per the BL-152 ADR.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-152 measured: `--http2` exits 2 with `curl: option --http2: the installed libcurl version does not support this` then `curl: try 'curl --help' or 'curl --manual' for more information`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `--http2`, `--http2-prior-knowledge` and `--http3` each exit 2 with the measured two lines.
- [ ] Each accepted option sets its `CommandLineOptions` member; tests per option.
- [ ] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
