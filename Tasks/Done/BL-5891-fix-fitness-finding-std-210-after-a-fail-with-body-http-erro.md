---
id: BL-5891
title: Fix fitness finding STD-210: after a --fail-with-body HTTP error, --retry opens a new connection where curl reuses the kept-alive one (%{num_connects} 1 vs 0)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.FailWithBodyConnection.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5891 — Fix fitness finding STD-210: after a --fail-with-body HTTP error, --retry opens a new connection where curl reuses the kept-alive one (%{num_connects} 1 vs 0)

## Goal

STD-210 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: after a --fail-with-body HTTP error, --retry opens a new connection where curl reuses the kept-alive one (%{num_connects} 1 vs 0). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed · **Area:** HTTP retry, connection reuse, write-out. Windows build.
- **Mock:** `/flaky/<key>/<n>/<code>` answers `<code>` with a body (keep-alive, Content-Length) for the first n hits, then 200; `/reset/<key>` clears the counter.
- **Repro:** `curl -s http://127.0.0.1:34000/reset/x`, then `curl -sv --retry 2 --retry-delay 1 --retry-all-errors --fail-with-body http://127.0.0.1:34000/flaky/x/1/500 -w "%{num_connects} %{num_retries}\n"`
- **Expected (REF):** `* Connection #0 … left intact`, then `* Reusing existing http: connection`, last line `0 1`.
- **Actual (.NET):** `* closing connection #0`, then a new connection (`Connection #1`), last line `1 1`. Data and exit code match. With plain `-f` both close the connection, as expected.
- **Likely location:** the HTTP fail/retry handling in the engine: the `--fail-with-body` path closes the connection although it has read the whole body.
- **Acceptance check:** the repro prints `Reusing existing http: connection` and ends with `0 1`.

## Acceptance criteria

- [x] the repro prints `Reusing existing http: connection` and ends with `0 1`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The cause was in the HTTP handler, not in `ManagedEasyTransfer.cs`: `--fail-with-body` threw
  after the body was read, so the attempt took the failure path and closed the connection.
  `touches` widened to `HttpProtocolHandler.cs` and its new test file; no task in Doing on
  `origin/work/dark-factory` named either (BL-5889 holds only the two content decoder files).
- Measured with `Record-CurlExchange.ps1` against curl 8.22.0 on Windows, a keep-alive 500 then a
  200, `-sv --retry 2 --retry-delay 1 --retry-all-errors --fail-with-body`: `Connection #0 ...
  left intact`, `Reusing existing http: connection with host 127.0.0.1`, stdout `oopsok0 1`.
  Without `--retry`, `-v` writes no `* The requested URL returned error: 500` line: it ends
  `left intact`, then `curl: (22) ...`. The tool raises this error, not libcurl.
- So the handler now treats such an exchange as one that ran whole (same connection end, no `*`
  error line) and only gives the exit 22 result. A `Connection: close` response therefore ends
  `shutting down connection #N` rather than `closing connection #N`, as a success does.
- No ADR: this only matches measured curl. Curl's own `-v` and `-w` output for both runs
  matched the above after the change.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 100% line and
  branch, 0 failing members. `dotnet format` reports an imports-order error in the untouched
  `Http2RequestHeaders.cs`, left to BL-5921.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 9d8d065a 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. --fail-with-body keeps the connection as curl does, so --retry reuses it
