---
id: BL-386
title: Send the -r text verbatim in HTTP Range and Content-Range
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0044-http-honours-range-resume-time-condition-and-max-filesize.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-386 — Send the -r text verbatim in HTTP Range and Content-Range

## Goal

HTTP `Range` and `Content-Range` carry the `-r` text as typed, as curl 8.21.0 does, so `-r 0-9,20-29` sends `Range: bytes=0-9,20-29` instead of `bytes=0-9`.

## Context

- Found in BL-306. Measured on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`: `curl -d x -r 0-9,20-29 URL` sends `Content-Range: bytes 0-9,20-29/1`. libcurl passes `data->state.range` (the option text) straight into both headers.
- The handler only sees `ITransferContext.Range`, the one `ByteRange` that `Curl.Core/ByteRangeParser.cs` reads the way `Curl_range` does for `file://` (`2-3,5-6` becomes `2-3`, `1-2abc` becomes `1-2`). The HTTP handler formats that parsed range (`HttpRangeHeader`), so a list or trailing text is lost. ADR-0044 "Costs and caveats" records the gap.
- Needs the contract to carry the raw text (e.g. `ITransferContext.RangeText`) set by `Curl.Console`, and a measurement of what curl sends for odd text (`1-2abc`, `abc`, `-0`) over HTTP before pinning, since `ByteRangeParser`'s exit-33 cases were measured over `file://` only.

## Acceptance criteria

- [x] `-r 0-9,20-29` sends `Range: bytes=0-9,20-29` on a GET and `Content-Range: bytes 0-9,20-29/1` with `-d x`, pinned in `HttpProtocolHandlerTests` from measured bytes.
- [x] The HTTP behaviour of `-r 1-2abc`, `-r abc` and `-r -0` is measured against curl 8.21.0, recorded in Notes, and pinned.
- [x] ADR-0044's caveat about the parsed range is removed or updated.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for every library changed.

## Notes

**Measured 2026-09-27**, curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1`
against an empty `200` with `Content-Length: 0`, `curl -s -S -r <text> http://127.0.0.1:18386/x`
and the same with `-d x`:

| `-r` | GET sends | `-d x` sends | exit |
| --- | --- | --- | --- |
| `0-9,20-29` | `Range: bytes=0-9,20-29` | `Content-Range: bytes 0-9,20-29/1` | 0 |
| `1-2abc` | `Range: bytes=1-2abc` | `Content-Range: bytes 1-2abc/1` | 0 |
| `abc` | `Range: bytes=abc` | `Content-Range: bytes abc/1` | 0 |
| `-0` | `Range: bytes=-0` | `Content-Range: bytes -0/1` | 0 |
| `3-1` | `Range: bytes=3-1` | `Content-Range: bytes 3-1/1` | 0 |
| `a-3` | `Range: bytes=a-3` | `Content-Range: bytes a-3/1` | 0 |
| `5-` | `Range: bytes=5-` | `Content-Range: bytes 5-/1` | 0 |

So HTTP never parses the text and never refuses it: `abc`, `-0` and `3-1`, exit 33 over
`file://`, go out as typed with exit 0. stderr was empty for all (the `-r abc` warning is
the parser's, suppressed by `-s`).

**Design** (decided by Claude under Stewart's delegation; recorded as an amendment to
ADR-0044 rather than a new ADR, so parallel lanes cannot collide on an ADR number):
- `ITransferContext.RangeText` (new) carries the `-r` text as given; `Range` stays the one
  parsed range the `file://` and FTP handlers serve. `TransferContextFactory` sets it from
  `options.Range`; `RedirectFollower` copies it to every hop.
- The HTTP handler reads only `RangeText`: `HttpRangeHeader` sends `bytes=<text>` and
  `bytes <text>/<length>`; `HttpRequestFraming` carries the text; `HttpDownloadConditions`
  skips the `-z` comparison whenever `RangeText` is set.
- `CurlCommandRunner.TryParseRange` no longer refuses unparseable text on an `http`/`https`
  URL (`Range` is `null` there, `RangeText` is not). An `ftp` URL forwarded through an HTTP
  proxy still gets exit 33 for such text; not measured, left as an ADR-0044 caveat.

**Pinned:** `HttpProtocolHandlerTests.Conditions` (GET `Range` and `-d x` `Content-Range` for
`0-9,20-29`, `1-2abc`, `abc`, `-0`), `HttpRangeHeaderTests`, `HttpRequestFramingTests`,
`CurlCommandRunnerTransferOptionTests.RunAsync_HttpRangeThatNamesNoRange_DispatchesTheTextAsTyped`
and `RunAsync_RangeList_DispatchesTheTextAsTypedAndTheFirstRangeParsed`.

**Touches:** added ADR-0044 (the acceptance criteria edit it); no task in `Doing` names it.

**Quality:** Core, Abstractions and Http 100/100 with no failing member. `Curl.Console` reports
two failing members, both in files this task did not change and both already filed:
`DiskWriteOutFileOpener.TryOpen` (BL-432) and `DumpHeaderOutputStream.WriteAsync` (BL-455).
Every member this task changed is at 100% line and branch.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. HTTP sends the -r text verbatim in Range and Content-Range (0-9,20-29, 1-2abc, abc, -0), never exit 33, as curl 8.21.0 does
