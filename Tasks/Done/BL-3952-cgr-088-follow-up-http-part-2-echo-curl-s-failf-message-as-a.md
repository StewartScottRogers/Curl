---
id: BL-3952
title: CGR-088 follow-up (HTTP part 2): echo curl's failf message as a -v line for bad gzip, a bad chunk and a short body
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3948]
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr088BodyTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3952 — CGR-088 follow-up (HTTP part 2): echo curl's failf message as a -v line for bad gzip, a bad chunk and a short body

## Goal

Under -v and --trace, a bad gzip body, a bad chunk and a short body write curl 8.22.0's failf message as a "* <message>" info line, in curl's order.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; split from BL-3880, which the earlier BL-3617 left (read BL-3617's Notes in Tasks/Done for the pattern: echo at the failing site, not in CurlCommandRunner, so lines already written do not repeat). Measure each path with real curl 8.22.0 through `Record-CurlExchange.ps1` before pinning; check the same line appears under `--trace`. Run Measure-CodeQuality.ps1 for the library before committing. Messages are in HttpTransferMessages.cs ("Error while processing content unencoding: ...", "transfer closed with outstanding read data remaining", "transfer closed with N bytes remaining to read"). Depends on BL-3948 only because both edit HttpProtocolHandler.cs.

## Acceptance criteria

- [x] Each path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [x] A focused unit test in Curl.Protocol.Http.UnitTests/Cgr088BodyTests.cs fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured real curl 8.22.0 (`Record-CurlExchange.ps1 ... -CurlArgs -sv`): short body -> `* end of response with 95 bytes missing`; cut-off chunked -> `* transfer closed with outstanding read data remaining`; `zz` chunk -> `* chunk hex-length char not a hex digit: 0x7a` then `* Illegal or missing hexadecimal sequence in chunked-encoding`; 17-digit size -> `* chunk hex-length longer than 16`, `* Too long hexadecimal number in chunked-encoding`; FFFFFFFFFFFFFFFF -> `* invalid chunk size: ...`, Illegal or missing...; `5
helloXX` -> `* Malformed encoding found in chunked-encoding`; bad gzip --compressed -> `* Error while processing content unencoding: incorrect header check`. Curl now writes the same lines at the same place (after the head, before `closing connection`); `--trace` shares the info channel so shows them too.
- Echo is in HttpProtocolHandler.ReportBodyFailure (inside touches); tests in Cgr088BodyTests.cs; one existing test (BodyCutShort) updated to the new transcript. Measure-CodeQuality.ps1 for Curl.Protocol.Http.UnitLibrary: 0 failing members. Platform-neutral (event text only). Decision (default): no ADR needed, same pattern as BL-3617/BL-3948.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 30167198 b91022d3 779790c0.
- 2026-10-28: Doing -> Done. -v echoes curl's failf line for a bad gzip, a bad chunk, a cut-off chunked body and a short body
