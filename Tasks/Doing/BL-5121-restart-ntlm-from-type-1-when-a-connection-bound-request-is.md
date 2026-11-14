---
id: BL-5121
title: Restart NTLM from Type-1 when a connection-bound request is resent on a fresh connection
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/NtlmRetryOnFreshConnectionTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5121 — Restart NTLM from Type-1 when a connection-bound request is resent on a fresh connection

## Goal

When an NTLM exchange over `--http1.0` loses its connection (the server closes without `Connection: close`) and the request is sent again on a fresh connection, the NTLM handshake restarts from Type-1 instead of resending the old Type-3 that is bound to the dead connection.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-071 (Low, Confirmed twice). Area: HTTP `--ntlm`, connection-bound auth.

**Repro.**
1. Run an NTLM mock (PowerShell or a C# file-based app, not Python) on 127.0.0.1:21010 that answers a Type-1 with a 401 carrying a valid Type-2 (flags `0xe2898215`, target info with domain DOM) and a Type-3 with 200. The Type-2 reply is HTTP/1.1 with no `Connection` header, and the server then closes the HTTP/1.0 connection.
2. `curl -s -v --ntlm -u user:pass --http1.0 http://127.0.0.1:21010/x -w "[%{http_code} %{num_connects}]"`

**Expected (upstream curl).** After the Type-3 fails on the closed socket, curl reconnects and starts again from Type-1; against this mock it loops and ends `curl: (56) Recv failure: Connection was aborted [401 6]`.

**Actual.** Opens a new connection and immediately sends the stale Type-3 (NTLM is connection-bound, so a real server rejects it); against the mock it ends `[200 2]`.

**Where to start.** The "pooled connection that dies before its response begins is sent again once on a fresh one" retry in `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`. When the request carries a connection-bound NTLM or Negotiate answer, restart the auth state from Type-1 on the fresh connection. The exact loop count (6 connects against the mock) need not be reproduced; the stale Type-3 must not be sent.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests/NtlmRetryOnFreshConnectionTests.cs` prove, with in-memory connection stand-ins, that when the connection dies after the Type-1/Type-2 exchange and before the Type-3 response begins, the first request on the fresh connection carries `Authorization: NTLM TlRMTVNTUAABAAAA` (a Type-1 message), not the earlier Type-3.
- [ ] A test proves the same restart for a Negotiate (connection-bound) answer, or records in the task's Log why the Negotiate path cannot reach this retry.
- [ ] A regression test proves a non-connection-bound request (for example Basic) is still resent unchanged on a fresh connection.
- [ ] Tests are platform-neutral (stand-in authenticator, no SSPI).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: on the fresh connection the command sends `Authorization: NTLM TlRMTVNTUAABAAAA...` (Type-1), not the old Type-3.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 071aee68 280cf044 e5910ad0 5e6bf6ef a0b49a29.
