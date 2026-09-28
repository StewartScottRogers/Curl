---
id: BL-396
title: Resend a custom Expect request after a 417 mid-upload the way curl loops to its redirect limit
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-319]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-396 — Resend a custom Expect request after a 417 mid-upload the way curl loops to its redirect limit

## Goal

A request with an `-H "Expect: 100-continue"` line that draws a 417 while its body is being sent is resent the way curl 8.21.0 resends it: the custom line again, a new wait, and another resend for each 417, until the redirect limit ends it with exit 47 `Maximum (50) redirects followed`.

## Context

- Measured in BL-319 (Notes): `curl -T big.bin -H "Expect: 100-continue"` against a server that answers every `Expect` request with 417 after 65536 body bytes made 51 connections, waited for `100 Continue` on each, and ended with exit 47, `%{num_connects}` 51, all 51 417 heads in `-D`.
- BL-319 resends once without the wait (`HttpRequestFraming.WithoutExpect`), so the second 417 arrives after the whole body and is the result, exit 0.
- Decide with a measurement whether `--max-redirs` bounds the loop and whether `-L` matters.

## Acceptance criteria

- [x] The loop is measured on curl 8.21.0, with and without `--max-redirs 3`, and the bytes are in Notes.
- [ ] A handler test replays it through the fakes and matches the measured connection count, exit code, message and `-D` bytes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports no failing member.

## Notes

### Measured on curl 8.21.0 (Schannel, mingw64), 2026-09-27

`Record-CurlExchange.ps1 -Connections 60 -RespondAfterBodyBytes 65536`, every connection
answered `HTTP/1.1 417 Expectation Failed\r\nContent-Length: 0\r\n\r\n` (54 bytes) once 65536
body bytes arrived. `big.bin` is 1048577 zero bytes. Every run used
`-T big.bin -D h -o out -w "%{http_code} %{size_request} %{size_upload} %{size_header} %{num_connects} %{num_redirects}"`.

| Extra arguments | Result |
| --- | --- |
| `-H "Expect: 100-continue"` | 51 connections, each waits for `100 Continue` again and draws the 417; exit 47, stderr `curl: (47) Maximum (50) redirects followed`, `-D` = 51 x the 54-byte 417 head (2754 bytes), stdout `417 19929472 393216 2754 51` (no `%{num_redirects}` in that run). |
| `-H "Expect: 100-continue" --max-redirs 3` | 4 connections, exit 47 `Maximum (3) redirects followed`, `-D` 216 bytes (4 heads), `417 2097664 524288 216 4 3`. |
| `-H "Expect: 100-continue" --max-redirs 0` | 1 connection, no resend, exit 47 `Maximum (0) redirects followed`, `-D` 54 bytes, `417 393344 393216 54 1 0`. |
| `-H "Expect: 100-continue" -L` | Same as without `-L`: 51 connections, exit 47 `Maximum (50)`, `417 23468416 524288 2754 51 50`. `-L` does not matter. |
| `-H "Expect: 100-continue" -L --max-redirs 3` | Same as without `-L`: `417 1966592 393216 216 4 3`, exit 47. |
| no `-H` (curl's own `Expect`), 417 then `200 ok` | The BL-319 resend: exit 0, `200 1573099 1048577 92 2 1`. **The resend counts as a redirect** (`%{num_redirects}` 1). |
| no `-H`, 417 then `200 ok`, `--max-redirs 0` | No resend: exit 47 `Maximum (0) redirects followed`, `417 524416 524288 54 1 0`. |

How many body bytes go before the 417 is seen is timing (65536 to 524288 above), as in BL-319;
the heads, connection count, exit code, message and `%{num_redirects}` are what the tests pin.
`--max-redirs -1` (no limit) was started but not recorded: the 60-connection server would end it.

### Why this task went back to Backlog (2026-09-27, lane 3)

`--max-redirs` bounds the loop, and curl counts every 417 resend as a followed redirect, the
single BL-260/BL-319 resend included. `HttpProtocolHandler` cannot see the limit: it lives in
`Curl.Core.UnitLibrary`'s `RedirectPolicy`, filled by `Curl.Console`, and nothing on
`ITransferContext`/`HttpRequestOptions` carries it. So the work needs, beyond the Http pair:

1. `Curl.Protocol.Abstractions.UnitLibrary`: a `MaxRedirects` on `HttpRequestOptions` (default 50,
   -1 unlimited) the handler reads, with its test.
2. `Curl.Console`: map `--max-redirs` into it.
3. `Curl.Core.UnitLibrary`: `RedirectFollower` hands each hop the budget left
   (`MaxRedirects - followed`) and adds the handler's `TransferReport.RedirectCount` to the chain's,
   since curl shares one counter between `-L` hops and resends.
4. `Curl.Protocol.Http.UnitLibrary`: a resend keeps an `-H` `Expect: 100-continue` wait
   (`HttpRequestFraming.WithoutExpect` must keep `AwaitsContinue` when the line is the user's),
   counts each resend into `RedirectCount`, and ends with exit 47
   `Maximum (N) redirects followed` - the 417 head written, no resend - once the count reaches the
   limit; `--max-redirs 0` refuses even curl's own single resend.

Those three projects are in BL-348's `touches` (in Doing on lane 1), so per the dark-factory
rule they were added to this task's `touches` and the task went back to Backlog; the board
offers it again once BL-348 is Done. Unmeasured and worth a check next run: whether a 417 during
the wait (BL-260, same connection) also counts as a redirect - curl uses the same retry path, so
it most likely does.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Needs Curl.Protocol.Abstractions.UnitLibrary, Curl.Core.UnitLibrary and Curl.Console to carry --max-redirs to the handler; BL-348 (Doing) touches all three
