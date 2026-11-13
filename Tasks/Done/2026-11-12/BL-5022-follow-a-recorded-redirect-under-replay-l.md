---
id: BL-5022
title: Follow a recorded redirect under --replay -L
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs, Curl.Extension.RecordReplay.UnitTests/RecordReplayTransferHookTests.cs, Documentation/Planning/Decisions/ADR-1135-record-and-replay-store-exchanges-in-curl-exchange-record-format-1.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5022 — Follow a recorded redirect under --replay -L

## Goal

`curl --replay <dir> -L <url>` follows a recorded 302 through the recording and prints the final recorded body with `%{http_code}` 200 and `%{num_redirects}` 1, with the server stopped.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-034 (Medium, Confirmed). Data loss / silent wrong operation: yes.

**Repro.** Run a server on 127.0.0.1:48000 where `/redir` answers `302 Location: /plain` and `/plain` answers `hello`. Run `curl -sS --record rr2 -L http://127.0.0.1:48000/redir -w "live: %{http_code} %{num_redirects}\n"`: it prints `hello` and `live: 200 1` and records two exchanges. Then run `curl -sS --replay rr2 -L http://127.0.0.1:48000/redir -w "[%{http_code} %{num_redirects}]\n"`.

**Expected.** ADR-1135, point 5: a request whose key has a recording is answered from it, and replay gives curl's output back unchanged. The replay prints `hello` and `[200 1]`.

**Actual.** Only `[302 0]` is printed: no body, exit 0, no warning. Reproduced three times with two recording directories.

**Where to start.** `Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs`. The replayed 302 is returned as the final result, so curl's redirect-following never runs through the replay lookup. If the follower lives in `Curl.Core.UnitLibrary` (see `RedirectFollower`), prefer making the hook answer each hop, not changing Core.

## Acceptance criteria

- [x] `Curl.Extension.RecordReplay.UnitTests/RecordReplayTransferHookTests.cs` has a test that records `/redir` (302 to `/plain`) and `/plain` (`hello`) under `-L`, then replays with no server and gets stdout `hello`, `%{http_code}` 200, `%{num_redirects}` 1 and exit 0.
- [x] A test shows a replay of a 302 whose `Location` target has no recording fails with a stated error (not a silent `302`, exit 0) or follows curl's rule for a missing recording as ADR-1135 states it; the chosen behaviour is written in ADR-1135 point 5.
- [x] Regression: a test shows `--replay` without `-L` still prints the recorded 302 as before (`[302 0]`), and a single non-redirect replay is unchanged (`ReplaySizeWriteOutTests` still pass).
- [x] ADR-1135 (`Documentation/Planning/Decisions/ADR-1135-record-and-replay-store-exchanges-in-curl-exchange-record-format-1.md`) states that replay under `-L` follows recorded redirects.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests should use the library's scripted/recorded fixtures, no real socket. If the fix turns out to need `Curl.Core.UnitLibrary` (the redirect follower), add that file to `touches` first and keep the Core change to one narrow file.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 228ce3e5 eea4f50d 4dc663c9 a4d60590 a0b49a29.

Notes added by lane 10: the replayed 3xx now reports its `Location` (resolved against the request URL) as `RedirectUrl`, so Core's follower asks the hook for each hop; no Core change. Missing hop recording keeps the existing exit 7 (ADR-1135 point 5 states it). Hook-level tests cover the follow, the missing target and the no-redirect cases; no-`-L` is unchanged because only RedirectUrl was added. Fast tests: all green except Curl.Conformance.Extended.UnitTests RequirementMatrixTests, which fails on SpecRequirement attributes of McpBatchArgumentRefusalTests and SignalRProtocolHandlerTests (other tasks' files, present before this change).
- 2026-11-12: Doing -> Done. Replay under -L follows recorded redirects
