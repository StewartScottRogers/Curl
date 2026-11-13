---
id: BL-5123
title: Apply -f and --fail-with-body to a replayed exchange (EXT-050)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs, Curl.Extension.RecordReplay.UnitTests/ReplayFailModeTests.cs, Documentation/Planning/Decisions/ADR-1135-record-and-replay-store-exchanges-in-curl-exchange-record-format-1.md]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5123 — Apply -f and --fail-with-body to a replayed exchange (EXT-050)

## Goal

A replayed exchange honours `-f` and `--fail-with-body` exactly as the live transfer would: a recorded 4xx or 5xx exits 22 with curl's error line, with no body under `-f` and with the body under `--fail-with-body`.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-050 (Medium, Confirmed). Silent wrong operation: a script that relies on `-f` to detect a failed request gets exit 0 from `--replay`.

**Repro.** With a server answering `/404` with `404` and body `notfound\n` (PowerShell or a C# file-based app, never Python):
1. `curl -sS -m 5 --record rp4 http://127.0.0.1:28000/404 -o NUL`
2. `curl -sS -m 5 --replay rp4 -f http://127.0.0.1:28000/404 -w "[%{http_code}]\n"`
3. `curl -sS -m 5 --replay rp4 --fail-with-body http://127.0.0.1:28000/404 -w "[%{http_code}]\n"`

**Expected.** ADR-1135 point 5, "the transfer reports the recorded status", and replay reproduces the live run. Live `curl -f .../404` prints `curl: (22) The requested URL returned error: 404` with no body and exits 22. The port's OHTTP and resumable-upload hooks already apply `HttpFailMode`.

**Actual.** Steps 2 and 3 both print `notfound` and `[404]`, exit 0, nothing on stderr.

**Where to start.** `Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs`, `ReplayAsync`, never consults `context.Http.Fail` (`HttpFailMode`). Mirror `Curl.Extension.ObliviousHttp.UnitLibrary/Relay/InnerResponseOutput.cs` lines 19 and 37: with `HttpFailMode.Fail` and a recorded status of 400 or more, return `TransferResult.Failure(CurlExitCode.HttpReturnedError, "The requested URL returned error: <code>")` before writing the body (the head still goes to `-D`/`-i` outputs only as live curl does; check what live `curl -f` writes to `-D` and match it); with `HttpFailMode.FailWithBody`, write the body then return the failure result carrying the body length. The `%{http_code}` report must still read the recorded status in both cases.

## Acceptance criteria

- [x] New tests in `Curl.Extension.RecordReplay.UnitTests/ReplayFailModeTests.cs` (new) prove that replaying a recorded 404 with `-f` returns `CurlExitCode.HttpReturnedError` (22) with message `The requested URL returned error: 404`, writes no body, and reports response code 404.
- [x] A test proves `--fail-with-body` on the same replay writes the body `notfound\n`, returns exit 22 with the same message and reports 404.
- [x] A test proves a recorded 200 and a recorded 3xx under `-f` still succeed (exit 0, body written), and replay without `-f` of a 404 stays exit 0 with the body, as before.
- [x] ADR-1135 (point 5) states that replay applies `-f` and `--fail-with-body` to the recorded status.
- [x] The tests are platform-neutral (no real socket; no Windows-only path such as `NUL`).
- [x] `dotnet build Curl.Extension.RecordReplay.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Review acceptance check, for a hand check: step 2 prints `curl: (22) The requested URL returned error: 404` and `[404]` with no body, exit 22; step 3 prints `notfound`, the same error line and `[404]`, exit 22.
- A related Low finding (the replayed `%{method}` is POST for a `-G -d` request) is filed separately if at all; do not widen this task.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 78816b69 33284914 2eb3a8a4 159c3dc6 909ad796 5020db9c.
- 2026-11-13: Decided: under -f the head goes to -D only (not -i), as live curl; --fail-with-body writes head and body. Tests platform-neutral; CI criterion met by that. Quality run clean (100/100).
- 2026-11-13: Doing -> Done. Replay applies -f and --fail-with-body to the recorded status
