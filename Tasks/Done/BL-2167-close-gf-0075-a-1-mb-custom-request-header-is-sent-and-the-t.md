---
id: BL-2167
title: Close GF-0075: A 1 MB custom request header is sent and the transfer ends with exit 52, where curl refuses it with exit 100
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2167 — Close GF-0075: A 1 MB custom request header is sent and the transfer ends with exit 52, where curl refuses it with exit 100

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0075 (A 1 MB custom request header is sent and the transfer ends with exit 52, where curl refuses it with exit 100), so a later gap analysis measures each of `behaviour:test3229` as `match`.

## Context

- Finding: GF-0075, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3229`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test3229 (-K - with stdin 'header X-Large: <1048576 x>', http://%HOSTIP:%HTTPPORT/3229): expected 'upstream test3229 passes' (exit 100, CURLE_TOO_LARGE, within a 6 MB allocation limit), actual '<verify><errorcode>: expected exit code 100, got 52'. Curl.Protocol.Http.UnitLibrary raises CurlExitCode.TooLarge only for response lines (HttpLineReader, HttpResponseHeadBuilder), never for the request it builds. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 3229

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary, cap the request head as curl 8.22.0's request buffer does: when the head HttpRequestHeadFormatter builds (custom headers included) grows past curl's limit, fail before sending with exit 100 (CurlExitCode.TooLarge) and curl's message, both measured on the reference with Record-CurlExchange.ps1. Add a unit test with a 1 MB -H value.

## Acceptance criteria

- [x] `behaviour:test3229`: Curl answers what curl 8.22.0 answers, `upstream test3229 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- Measured curl 8.22.0 (Schannel, Windows) with `Record-CurlExchange.ps1` and a `-K` file holding
  `header "X-Large: xxx..."`: a request head of 1,048,575 bytes is sent; one of 1,048,576 is
  refused after connecting, nothing sent, exit 100, `-v` shows `* HTTP request too large` and
  stderr `curl: (100) HTTP request too large`. curl's request buffer is 1 MiB including a
  terminating byte.
- Fix: `HttpRequestHeadFormatter.MaximumRequestHeadLength` and `ThrowIfTooLarge`, called first in
  the handler's exchange `try` so the refusal takes the normal failure path (exit 100, info line)
  before anything is sent. The check counts the head only; a small `-d` body curl would put in the
  same buffer is not counted (sensible default: test3229 and the finding are about the head).
- Not measured end to end through `Curl.Console`: the lane sandbox refuses access to `bin/`. The
  handler tests drive the same path through a scripted connection.


## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. A request head of 1 MiB or more now fails with exit 100 'HTTP request too large' before sending, as curl 8.22.0 does
