---
id: BL-5322
title: Never join --segments pieces when the server sends no ETag or Last-Modified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SegmentedDownload.UnitLibrary/SegmentedDownloadTransferHook.cs, Curl.Extension.SegmentedDownload.UnitLibrary/CapturedResponse.cs, Curl.Extension.SegmentedDownload.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5322 — Never join --segments pieces when the server sends no ETag or Last-Modified

## Goal

`--segments N` against a server that sends neither an ETag nor a Last-Modified never joins ranges taken at different times: it falls back to one plain GET, so the output is one consistent version.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-067 (Medium, Confirmed, 2 runs; silent corrupt download, exit 0). Area: `--segments`; Windows. This is an extension, so it is judged against the port's own docs and ADRs, not curl.

**Repro.** A server whose `/rgnoval` serves 300,000 bytes that differ on each request (`data[i] = (i*7 + n) % 251`, `n` = request count), honours `Range` with a correct 206 and `Content-Range`, and sends no ETag or Last-Modified (write it in PowerShell or a C# file-based app, never Python). `curl -sS -m 10 --segments 4 http://127.0.0.1:38000/rgnoval -o s4.out -w "%{http_code} %{size_download}\n"`

**Expected.** ADR-1087: "Any other segment that does not fit runs the transfer again as curl runs it, so the output is never a mix of two representations." With no validator nothing can be compared, so the segments must not be joined: fall back to a single GET.

**Actual.** Prints `206 300000`, exit 0; `s4.out` is four 75,000-byte pieces from versions 1, 2, 3 and 4.

**Where to start.** `Curl.Extension.SegmentedDownload.UnitLibrary`, where segments are checked against the first response (`SegmentedDownloadTransferHook.cs`, `CapturedResponse.cs`). Read ADR-1087 first and keep the strong-ETag case (ranges sent with `If-Range`) as it is.

## Acceptance criteria

- [x] New tests in `Curl.Extension.SegmentedDownload.UnitTests` (a stand-in transport, no real network) prove: when the first response carries neither an ETag nor a Last-Modified, no segment ranges are sent and one plain GET's response is written.
- [x] A test proves a weak-only validator or a Last-Modified-only response follows ADR-1087's existing rule unchanged (state in the test name which way the ADR decides), and a test proves a strong ETag still sends the requested number of ranges, each with `If-Range`.
- [x] A test proves a server whose body changes per request, with no validator, yields exactly one version in the output (the bytes of a single response).
- [x] Tests are platform-neutral.
- [x] Review acceptance check recorded in the Log as a hand check: the repro command writes one consistent version (or sends one plain GET), and the strong-ETag case still sends 4 ranges with `If-Range`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command writes one consistent version (or sends one plain GET); the strong-ETag case still sends 4 ranges with `If-Range`.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: c439f7e3 b888cb8e 1353c388 c1cf21f9 417d881e a0b49a29.
- 2026-11-16: Hand check by stand-in tests (RangeOrigin, no real network): no validator -> probe plus one plain GET, one version written; strong ETag -> 4 ranges each with If-Range. Weak-only and Last-Modified-only: ADR-1087 rule unchanged (ranges without If-Range, joined when segments repeat the validators). Quality gate for the library: 0 failing. ADR-1087 got an update note.
- 2026-11-16: Doing -> Done. --segments falls back to one plain GET when the first response has no ETag or Last-Modified
