---
id: BL-4949
title: Accept --no- forms of boolean extension flags
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary, Curl.Cli.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4949 — Accept --no- forms of boolean extension flags

## Goal

Every boolean extension flag has a `--no-` form, so a flag set in `.curlrc` or `-K` can be switched off per run.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-027 (Low, confirmed: reproduced at least twice).

- Repro: `curl --no-sse http://127.0.0.1:18800/plain`; also `--no-client-hints`, `--no-content-digest`, `--no-structured-fields`, `--no-ratelimit-pace`, `--no-resumable-upload`, `--no-graphql-introspect`, `--no-quic-migrate`.
- Expected: Accepted, flag off (curl's boolean-option convention).
- Actual: `curl: option --no-sse: is unknown`, exit 2.
- Where to start: Extension flag registration in `ExtensionOptionCatalogue` and the CLI lookup. `--ai-help` must list the `--no-` forms.

## Acceptance criteria

- [x] With `sse` in config file `cfg`, `curl -K cfg --no-sse http://127.0.0.1:18800/plain` prints `hello`.
- [x] Each listed `--no-` form is accepted.
- [x] `--ai-help` and `--extended-help` show the `--no-` forms.
- [x] With `CURL_EXTENSIONS=0` the `--no-` forms are unknown options, like the flags themselves.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Decided: a flag extension option is negated in the parser (`NegateExtensionFlag`) by forgetting its value; value options and unknown names stay unknown; listings show `--x, --no-x`. Verified by unit tests and the quality run (0 failing members).

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 6928193b 76b07451 a7ad9bc2 51b584e0 a0b49a29.
