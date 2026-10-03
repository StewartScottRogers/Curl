---
id: BL-1331
title: Refuse an HTTP/1.x reply with a NUL byte in a header or a second different Location header with exit 8, as curl does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: FR-078
created: 2026-10-03
completed: 2026-10-03
---
# BL-1331 — Refuse an HTTP/1.x reply with a NUL byte in a header or a second different Location header with exit 8, as curl does

## Goal

An HTTP/1.x response header line holding a NUL byte fails the transfer with exit 8 and `Nul byte in header`, and a second `Location` header whose value differs from the first fails it with exit 8 and `Multiple Location headers`, each before the offending line is reported as a response header and with the message reported as a `-v` info line, as curl 8.21.0 does.

## Context

- Today `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadBuilder.cs` (`AddLine`, line 67) refuses only a header line with no colon (exit 8 `Header without colon`, `HttpTransferMessages.HeaderWithoutColon`); neither text above exists anywhere in the solution, so a NUL byte is kept in the header and a second `Location` is accepted.
- curl 8.21.0 (tag `curl-8_21_0`):
  - `lib/http.c` lines 3815-3823: `memchr(hd, 0x00, hdlen)` on each header line, then `failf(data, "Nul byte in header")`, `CURLE_WEIRD_SERVER_REPLY`.
  - `lib/http.c` `http_header_l`, lines 3377-3395: an empty `Location` value, or an exact repeat of the one already kept, is ignored; any other second value is `failf(data, "Multiple Location headers")`, `CURLE_WEIRD_SERVER_REPLY`. This runs for every status code, with or without `-L`.
- Measured 2026-10-03 with the installed curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`:
  - `-Response 'HTTP/1.1 200 OK\r\nLocation: /a\r\nLocation: /b\r\nContent-Length: 2\r\n\r\nok' -CurlArgs '-sv',...`: exit 8, stdout empty; stderr `< HTTP/1.1 200 OK`, `< Location: /a`, `* Multiple Location headers`, `* closing connection #0`. The second `Location` line is not echoed.
  - `-Response 'HTTP/1.1 200 OK\r\nX-A: a\x00b\r\nContent-Length: 2\r\n\r\nok'`: exit 8, stdout empty; stderr `< HTTP/1.1 200 OK`, `* Nul byte in header`, `* closing connection #0`.
- Add both texts to `HttpTransferMessages` beside `HeaderWithoutColon`, and refuse where that one is refused so the order of checks matches curl's (NUL first, as `http_rw_hd` checks it before the header is parsed). HTTP/2 and HTTP/3 heads are not this task's.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Http.UnitTests` feeds the NUL-byte response above through the handler's fake connection and asserts exit 8 (`CurlExitCode.WeirdServerReply`), message `Nul byte in header`, nothing written to the output, the status line reported as a response header and the `X-A` line not.
- [x] A test feeds the two-`Location` response and asserts exit 8 with `Multiple Location headers`, `Location: /a` reported as a response header and `Location: /b` not, nothing written to the output.
- [x] Tests pin that `Location: /a` twice, and `Location: /a` followed by an empty `Location:`, are accepted (exit 0, body `ok`).
- [x] A test pins the two-`Location` refusal on a `302` under `-L` too: no second request is sent.
- [x] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Notes

- Delivered directly (a small change in one class): `HttpResponseHeadBuilder.AddLine` refuses a line holding a NUL first (any line, continuation included), then runs the existing no-colon check, then `KeepLocation`: the exchange keeps its first non-empty `Location` value; an empty value or an exact (ordinal) repeat is ignored, and any other value throws exit 8 `Multiple Location headers`. Both throw before the line is held, so the offending line is never reported and the headers before it are, as measured.
- Choice: the Location check uses the line's own value, not a folded one, so a `Location` folded over several lines is compared by its first line only. This edge case was not measured.
- Choice: no test pins a NUL in a continuation line, because which earlier lines curl echoes there was not measured. The refusal itself covers that case.
- Tests: `HttpProtocolHandlerTests.WeirdHeaderLine.cs` (6 cases). Http tests: 1765 passed, 4 skipped (platform).
- Quality: `HttpResponseHeadBuilder` and `HttpTransferMessages` are clean. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` still reports 5 failing members, all already failing before this task and in code it did not touch (`SettleConnection`, `ExchangeAsync`, `HttpResponseHeadReader..ctor`, `HttpRequestBodyWriter.ReportStreamRead`, `Http3StreamConnection..ctor`). They are filed as BL-1335 instead of widening this task, so the last criterion is ticked for this task's own members only.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. An HTTP/1.x head with a NUL byte in a line, or a second different Location, fails with exit 8 as curl 8.21.0 does
