---
id: BL-5092
title: Report an HTTPS proxy that drops TLS during CONNECT as exit 56, not 2
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HttpProxyTunnel.cs, Curl.Networking.UnitTests/HttpProxyTunnelMissingCloseNotifyTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5092 — Report an HTTPS proxy that drops TLS during CONNECT as exit 56, not 2

## Goal

When an HTTPS proxy closes the connection during CONNECT with no reply and no TLS close_notify, curl exits 56 with the schannel "server closed abruptly" message, as upstream does, instead of exit 2.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-056 (Low, Confirmed twice). Area: HTTPS proxy, CONNECT tunnel, Windows.

**Repro.** Run an HTTPS proxy on 127.0.0.1:22200 with a valid localhost certificate from `ca.pem`; on `CONNECT nonexist.invalid:443` it closes the TCP socket with no reply and no TLS close_notify. Then `curl -sS https://nonexist.invalid:443/ -x https://localhost:22200 --proxy-cacert ca.pem`.

**Expected (upstream curl).** `curl: (56) schannel: server closed abruptly (missing close_notify)`, exit 56. (With a plain `-x http://` proxy both print `(56) Proxy CONNECT aborted`.)

**Actual.** `curl: (2) schannel: server closed abruptly (missing close_notify)`, exit 2.

**Where to start.** `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`, `ReadReplyAsync` (about lines 182-206) catches only `IOException` with a socket error, so `MissingCloseNotifyException` reaches a generic handler that gives exit 2.

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests/HttpProxyTunnelMissingCloseNotifyTests.cs` prove that a missing-close_notify failure while reading the CONNECT reply is reported with `CurlExitCode` 56 (RecvError) and the message `schannel: server closed abruptly (missing close_notify)`, using an in-memory connection stand-in, not a network.
- [ ] A regression test proves the plain-proxy "Proxy CONNECT aborted" exit 56 and the existing socket-error handling are unchanged.
- [ ] The tests are platform-neutral (no Windows-only error text outside an `[OSCondition]`-marked test).
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the repro command exits 56 with the same stderr line.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 48fc9223 dd02aed2 c3c68286 0aaa6f77.
