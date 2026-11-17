---
id: BL-5409
title: Fail a nats ?request promptly when the server reports no responders
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsConnect.cs, Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs, Curl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5409 — Fail a nats ?request promptly when the server reports no responders

## Goal

`nats://host/subject?request` against a server with no subscriber on the subject fails within about a second with a message naming "no responders" and a non-zero exit, instead of waiting until `-m` (or forever).

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-082 (Low, Confirmed twice). Area: nats, natss.

**Repro.** `docker run -d --name n -p 127.0.0.1:48222:4222 nats:2-alpine --auth s3cret`; then `timeout 15 curl -sS -u s3cret: -d ping "nats://127.0.0.1:48222/nobody?request"; echo $?`, and the same with `-m 6`.

**Expected.** `--extended-help`: "with ?request publishes it with a reply subject and writes the one reply". NATS 2.2+ answers a request with no subscribers with a 503 "no responders" status when CONNECT sets `headers:true, no_responders:true`. Fail promptly with a clear message and non-zero exit.
**Actual (PORT).** Without `-m` it never exits (`timeout` exit 124); with `-m 6`, `curl: (28) Operation timed out after 6011 milliseconds with 0 bytes received`. With a responder it works.
**Where to start.** `Curl.Extended.Protocol.Nats.UnitLibrary/NatsConnect.cs` (the CONNECT line lacks `headers` and `no_responders`); `NatsSession.cs` (`RequestLines`, `IsDoneAfterAsync`: no HMSG 503 handling) with `NatsMessageLine.cs` and `NatsWireReader.cs` (HMSG parsing). The exit code is Claude's decision under Stewart's delegation: use 56 (receive failure) unless the other NATS errors use a different code, in which case match them; record the choice in an ADR if it is not obvious.

## Acceptance criteria

- [x] New tests in `Curl.Extended.Protocol.Nats.UnitTests` (a new file) prove the CONNECT line carries `"headers":true` and `"no_responders":true`.
- [x] A test with a scripted server that answers the request with an `HMSG` status 503 proves the transfer ends at once with a non-zero exit and a message containing "no responders", without waiting for a timeout (a fake `TimeProvider` is not advanced).
- [x] A test proves a request with a responder still writes the one reply and exits 0 (no regression), and a plain publish (no `?request`) is unchanged.
- [x] Tests are platform-neutral.
- [x] Hand check recorded in the Log (real `nats:2-alpine` in docker): the command exits non-zero within about 1 s with a message naming "no responders".
- [x] `--extended-help` for nats is checked and kept right; `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command exits non-zero within about 1 s with a message naming "no responders".

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: c520739f 86743a24 0201505b c1cf21f9 57f3c642 35275efa.
- 2026-11-17: Hand check, real `nats:2-alpine` in docker: `curl -sS -u s3cret: -d ping nats://127.0.0.1:48222/nobody?request` prints `curl: (56) NATS request failed: no responders are subscribed to the subject`, exit 56 (before the fix: timeout 124). Wall clock 2 to 8 s on this loaded host, but a plain publish to the same server took 4 s too, so the time is host startup, not waiting for a reply.
- 2026-11-17: Done. Exit 56 matches the other mid-transfer NATS failures (RecvError); no ADR needed. CONNECT carries headers and no_responders only for ?request, so publish and subscribe bytes are unchanged. HMSG is now parsed (headers skipped, payload written). Quality run: Nats library 100% line and branch, nothing failing. `--extended-help` text updated.
