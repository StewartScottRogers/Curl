---
id: BL-5136
title: Refuse --ws-repl combined with --ws-subprotocol or --ws-extended-connect with exit 2
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs, Curl.Extension.Composition.UnitTests/WebSocketReplCombinationTests.cs, Curl.Extension.WebSocketRepl.UnitLibrary/WebSocketReplExtension.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5136 — Refuse --ws-repl combined with --ws-subprotocol or --ws-extended-connect with exit 2

## Goal

`--ws-repl` given with `--ws-subprotocol` or `--ws-extended-connect` exits 2 naming both options, instead of silently dropping the second one.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-057 (Low, Confirmed). Part B: judged against the port's own docs and specs, not curl.

**Repro.** With any HTTP server on 127.0.0.1:28000 that logs requests:
1. `curl -sS -m 3 --ws-repl --ws-subprotocol "v12.stomp" ws://127.0.0.1:28000/ws`
2. `curl -sS -m 3 --ws-repl --ws-extended-connect h2 ws://127.0.0.1:28000/ws`

**Expected.** ADR-1081 says the REPL leaves out subprotocols because "no option asks for them", but `--ws-subprotocol` does. The port refuses other contradictory extension pairs with exit 2 (ADR-1262, ADR-1014; see `Curl.Extension.Composition.UnitLibrary/BodyReplacingHookExclusivity.cs` for the existing exclusivity pattern). Each combination should exit 2 naming both options: `--ws-repl and --ws-subprotocol cannot be used together` (or the same for `--ws-extended-connect`).

**Actual.** Both send a plain HTTP/1.1 `GET /ws` with `Upgrade: websocket`, no `Sec-WebSocket-Protocol`, and no HTTP/2 extended CONNECT; both end with `curl: (8) Refused WebSocket upgrade: ...` against this server, with no warning.

**Where to start.** `Curl.Extension.WebSocketRepl.UnitLibrary` (`Transfer/WebSocketReplTransferHook.cs` takes over without calling next); add an exclusivity check in `Curl.Extension.Composition.UnitLibrary`, following `BodyReplacingHookExclusivity.cs`. An extension must not reference another extension, so the check lives in Composition (which knows them all), not in the REPL library.

## Acceptance criteria

- [ ] A new test file `Curl.Extension.Composition.UnitTests/WebSocketReplCombinationTests.cs` proves `--ws-repl` with `--ws-subprotocol <x>` and `--ws-repl` with `--ws-extended-connect <x>` each fail before any connection with `CurlExitCode` 2 and the exact message `--ws-repl and --ws-subprotocol cannot be used together` (resp. `--ws-extended-connect`).
- [ ] Regression tests prove `--ws-repl` alone, `--ws-subprotocol` alone and `--ws-extended-connect` alone are not refused, and the existing exclusivity tests still pass.
- [ ] `--extended-help` for the REPL extension states the incompatibility; `--ai-help` is kept right if it mentions `--ws-repl`.
- [ ] Tests are platform-neutral and use no network.
- [ ] `dotnet build Curl.Extension.Composition.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python. The unit tests need no server.
- Extension isolation holds: no extension references another extension or an Original protocol library.
- Add any extra file the run really needs (for example the REPL extension description or ADR-1081) to `touches` before editing it.
- Review acceptance check, as a hand check: both commands exit 2 with the message above, and the server sees no request.

## Log

- 2026-11-13: Created.
