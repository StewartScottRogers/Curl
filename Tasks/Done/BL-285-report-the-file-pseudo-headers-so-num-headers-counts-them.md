---
id: BL-285
title: Report the file:// pseudo-headers so %{num_headers} counts them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-285 — Report the file:// pseudo-headers so %{num_headers} counts them

## Goal

The file:// handler returns a `TransferReport` whose `ResponseHeaders` hold the three pseudo-headers curl 8.21.0 counts, so `%{num_headers}` prints 3 as curl does.

## Context

- Found by BL-225. Measured 2026-09-26: `curl -s -o NUL -D - -w "[%header{Content-Length}][%{num_headers}]" file:///C:/Windows/win.ini` wrote `Content-Length: 92`, `Accept-ranges: bytes`, `Last-Modified: Mon, 01 Apr 2024 07:24:04 GMT`, then `[][3]`. So curl counts three headers but `%header{Content-Length}` finds none - measure why before choosing what the report carries (curl's header API may file them under a different origin).
- Today the file handler returns no report, so `TransferWriteOutVariables` prints `%{num_headers}` as 0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009, ADR-0018) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] A file:// transfer's `%{num_headers}` is 3 and its `%header{Content-Length}` empty, as measured on curl 8.21.0, pinned in a file-handler test.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.File`.

## Notes

### Measured 2026-09-26, curl 8.21.0 (x86_64-w64-mingw32, Schannel), `/mingw64/bin/curl`

File `C:/bl285tmp/a.txt` holding `hello world
` (12 bytes), URL `file:///C:/bl285tmp/a.txt`:

| Command | `-w` output |
| --- | --- |
| `curl -s -o NUL -D - -w "[%header{Content-Length}][%header{Accept-ranges}][%header{Last-Modified}][%{num_headers}]
%{header_json}
" URL` | headers `Content-Length: 12
Accept-ranges: bytes
Last-Modified: <date> GMT

`, then `[][][][3]
{
}
` |
| same without `-D -` | `[][3][{
}]` |
| `-I` | `[][3]` |
| `-r 0-2` | `[3]` |
| `-C 100` (past the end, exit 36) | `[3]` |
| `-z "Sat, 01 Jan 2099 00:00:00 GMT"` (unmet, exit 0) | `[0]` |
| missing file (exit 37) | `[0]` |
| `-T a.txt file:///C:/bl285tmp/b.txt` (upload) | `[0]` |
| `file:///dev/stdin` | `[0]` |

(`-z "2099-01-01"` is not a date curl parses, so it ran unconditioned and printed `[3]`; not a finding.)

**Why.** curl's `%{num_headers}` counts the header lines its tool header callback receives,
while `%header{}` and `%{header_json}` read libcurl's header API (`curl_easy_header`), which
only the HTTP family fills. `lib/file.c` writes its pseudo-headers as client header writes and
never files them in the header API, so every `%header{}` is empty and `header_json` is `{}`
while `num_headers` is 3 - whenever the headers were produced, `-o`/`-D` or not.

**Why this cannot be done inside `touches`.** `Curl.Output.UnitLibrary`'s
`TransferWriteOutVariables` derives both `num_headers` (`ResponseHeaders.Count`) and
`FindFirstHeaderValue` (`%header{}`) from `TransferReport.ResponseHeaders`. Putting the three
pseudo-headers there would make `%header{Content-Length}` print `12`, which curl does not.
Renaming or disguising the keys to dodge the lookup would break "say what it does".

**Design chosen (Decided by Claude under Stewart's delegation; ADR to be written with the code).**
Add `TransferReport.PseudoHeaders` (`IReadOnlyList<KeyValuePair<string,string>>`, default
empty): header lines a handler wrote to the header stream that curl's header API does not
hold (file:// today, FTP's `-I` lines later). `TransferWriteOutVariables` prints
`num_headers` as `ResponseHeaders.Count + PseudoHeaders.Count`; `%header{}` and
`header_json` keep reading `ResponseHeaders` only. `FileProtocolHandler` sets the three
pseudo-headers on the report whenever `WriteHeadersAsync`'s lines were produced (after an
unmet `-z` and before the open succeeded it sets none), whether or not `HeaderOutput` is set.
No change to `Curl.Protocol.Http.UnitLibrary`, which BL-178 holds.

**Touches widened** to `Curl.Protocol.Abstractions.UnitLibrary/UnitTests` (no task in Doing
names them) and `Curl.Output.UnitLibrary/UnitTests`, which BL-284 in Doing names, so the task
goes back to Backlog until BL-284 finishes.

### Delivered 2026-09-27

- `TransferReport.PseudoHeaders` added; `%{num_headers}` is `ResponseHeaders.Count + PseudoHeaders.Count`; `%header{}` still reads `ResponseHeaders` only. Recorded as ADR-0051.
- `FileTransferMessages.PseudoHeaders` gives the pairs; `PseudoHeaderLines` is now built from them, so written and reported headers cannot drift.
- `FileProtocolHandler.WithPseudoHeaders` attaches the report after the header stage, on success and on a body failure (the measured `-C 100` exit 36 case). The report also sets `DownloadSize` to the bytes transferred, because a report replaces `BytesTransferred` as the source of `%{size_download}` - without it `file://` `%{size_download}` would have dropped to 0.
- **Default taken:** a header output that fails mid-block (exit 23) reports no pseudo-headers. Not measured; the conservative choice, pinned in `ExecuteAsync_HeaderOutputFails_ReportsNoHeaders`.
- **Touches widened** to `Documentation/Planning/Decisions` for ADR-0051 and its index row; no task in Doing names it.
- Tests: `FileProtocolHandlerPseudoHeaderTests` (9 cases, every measured row of the table above), `TransferWriteOutVariablesTests.TryGetVariableText_PseudoHeaders_CountTowardsNumHeadersButAreNeverFound`, and `TransferReportTests` default/init. `Measure-CodeQuality.ps1`: File, Output and Abstractions 100% line and branch, 0 failing members.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Needs Curl.Output.UnitLibrary (and Curl.Protocol.Abstractions.UnitLibrary) for a PseudoHeaders report member; Curl.Output.UnitLibrary is held by BL-284 in Doing
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. file:// reports its pseudo-headers, so %{num_headers} is 3 and %header{Content-Length} empty as on curl 8.21.0
