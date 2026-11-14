---
id: BL-5139
title: Record the GET method for a -G -d request under --record and --replay
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.RecordReplay.UnitLibrary/ExchangeKey.cs, Curl.Extension.RecordReplay.UnitTests/ExchangeKeyGetFlagTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5139 — Record the GET method for a -G -d request under --record and --replay

## Goal

`--record` and `--replay` treat a `-G -d` request as the GET it is: the recorded method, the exchange key and `%{method}` on replay are all GET.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-059 (Low, Confirmed). Part B: judged against the port's own docs and specs, not curl.

**Repro.**
1. `curl -sS -m 5 --record rg2 -G -d a=1 http://127.0.0.1:28000/echo -o NUL -w "live %{method}\n"` prints `live GET`.
2. `curl -sS -m 5 --replay rg2 -G -d a=1 http://127.0.0.1:28000/echo -o NUL -w "replay %{method}\n"`
3. `sed -n 2p rg2/*.exchange`

**Expected.** ADR-1135: replay reports the recorded method and gives the live run's output; the live run sent `GET /echo?a=1`.

**Actual.** Step 2 prints `replay POST`; the exchange file's line 2 is `POST http://127.0.0.1:28000/echo?a=1`, and the key hashes the `-d` bytes that were never sent as a body.

**Where to start.** `Curl.Extension.RecordReplay.UnitLibrary/ExchangeKey.cs`: the method rule ("POST for a -d or -F body") ignores `-G`. With `-G`, curl moves `-d` data into the URL query and sends no body, so the key must neither choose POST nor hash those bytes as a body. Check how the key reads the request (the transfer hook, `RecordReplayTransferHook.cs`) to see whether `-G` is visible at that point.

## Acceptance criteria

- [ ] A new test file `Curl.Extension.RecordReplay.UnitTests/ExchangeKeyGetFlagTests.cs` proves a `-G -d a=1` request gets method GET, the recorded exchange line 2 reads `GET http://127.0.0.1:28000/echo?a=1`, and its key does not include the `-d` bytes as a body.
- [ ] A test proves a replay of such a recording reports `%{method}` as GET, and that a recording made with `-d a=1` without `-G` is still POST (regression, key unchanged).
- [ ] If an existing recording made before the fix no longer matches, the behaviour is stated in the extension's documentation or ADR-1135 note.
- [ ] Tests are platform-neutral.
- [ ] `dotnet build Curl.Extension.RecordReplay.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python. The unit tests need no server.
- Extension isolation holds: the extension references only `Curl.Extension.Abstractions` and shared layers.
- Review acceptance check, as a hand check: step 2 prints `replay GET` and the exchange file records `GET http://127.0.0.1:28000/echo?a=1`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 4e51009f 5daa8112 ff26e4a2 c1cf21f9 c263594a.
