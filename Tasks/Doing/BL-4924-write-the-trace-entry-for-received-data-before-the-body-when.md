---
id: BL-4924
title: Write the trace entry for received data before the body when both share stdout
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/TransferEventOutput.cs, Curl.Console.UnitTests/TraceAndBodyOrderTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4924 — Write the trace entry for received data before the body when both share stdout

## Goal

With `--trace-ascii -`, `--trace -` or `-v --stderr -`, the `<= Recv data` entry (or `{ [n bytes data]`) comes before the body, as in curl.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-014 (Low, confirmed: reproduced at least twice).

- Repro: `GET /a` returns `hello /a\n`. `curl -s --trace-ascii - http://127.0.0.1:18100/a` and `curl -s -v --stderr - http://127.0.0.1:18100/a`.
- Expected: REF 8.22.0: `<= Recv data, 9 bytes (0x9)` / `0000: hello /a.` / `hello /a` / `* Connection #0 to host 127.0.0.1:18100 left intact`; with `-v`: `{ [9 bytes data]` then `hello /a`.
- Actual: `hello /a` first, then the Recv data entry, for every URL of a multi-URL run (`file://` is right).
- Where to start: `HttpResponseBodyReader` reports before writing, so the swap is downstream: likely the `-` trace target in `Curl.Console/TransferEventOutput.cs` line 163 (`TraceTransferEventWriter`) is buffered separately from the body's stdout stream.

## Acceptance criteria

- [ ] `curl -s --trace-ascii - http://127.0.0.1:18100/a | tail -3` prints `0000: hello /a.`, `hello /a`, `* Connection #0 to host 127.0.0.1:18100 left intact`.
- [ ] With `-v --stderr -`, `{ [9 bytes data]` precedes `hello /a`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 37edc817 39ab3378 a0b49a29.
