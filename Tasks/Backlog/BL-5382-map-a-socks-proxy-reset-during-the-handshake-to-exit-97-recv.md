---
id: BL-5382
title: Map a SOCKS proxy reset during the handshake to exit 97 Recv failure
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SocksProxyTunnel.cs, Curl.Networking.UnitLibrary/Socks5Handshake.cs, Curl.Networking.UnitLibrary/Socks4Handshake.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5382 — Map a SOCKS proxy reset during the handshake to exit 97 Recv failure

## Goal

A SOCKS4, SOCKS4a, SOCKS5 or SOCKS5h proxy that resets the connection during the handshake gives `curl: (97) Recv failure: Connection was reset` and exit 97 (and `-w '%{exitcode}'` prints `97`), and a `--next` run carries on with the next URL.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-094 (Medium, Confirmed: 5 proxy variants, each at least twice; wrong exit code that is now run-ending, so later URLs are silently not attempted). Area: SOCKS proxy handshake, Windows.

**Repro.**
1. A TCP server on 127.0.0.1:41096 that reads the SOCKS5 greeting, replies `05 00`, reads the CONNECT request, then closes with `SO_LINGER {1,0}` (TCP RST). For SOCKS4, a server on 41098 that reads one packet and then sends RST. (PowerShell or a C# file-based app.)
2. Any HTTP server on 127.0.0.1:41000 serving `/hello`.
3. `curl -sS http://127.0.0.1:41000/hello -x socks5://127.0.0.1:41096 -w '[%{exitcode}]\n'` (also `socks5h://`)
4. `curl -sS http://127.0.0.1:41000/hello -x socks4://127.0.0.1:41098 -w '[%{exitcode}]\n'` (also `socks4a://`)
5. `curl -sS http://127.0.0.1:41000/hello -x socks5://127.0.0.1:41096 --next http://127.0.0.1:41000/hello -w '[%{exitcode}]\n'`

**Expected (REF).** `curl: (97) Recv failure: Connection was reset`, stdout `[97]`, exit 97. In step 5 the second URL runs and prints `hello[0]`; final exit 0.
**Actual (PORT).** `curl: (2) Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..`, no `-w` output, exit 2. In step 5 the run stops after the first URL. An HTTP proxy that resets gives exit 56, matching REF.
**Where to start.** `Curl.Networking.UnitLibrary/SocksProxyTunnel.cs` `ReadExactlyAsync` (~line 72) and its callers in `Socks5Handshake.cs` / `Socks4Handshake.cs`: the `IOException`/`SocketException` is not mapped to CURLE_PROXY (97) "Recv failure: Connection was reset" and falls through to exit 2.

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests` (a new file) prove a connection reset while reading the SOCKS5 CONNECT reply, and while reading the SOCKS4 reply, surface as exit 97 with message `Recv failure: Connection was reset`.
- [ ] A test proves the same for `socks4a` and `socks5h`, and for a reset during the SOCKS5 greeting reply.
- [ ] A test proves a clean early close (end of stream, no reset) keeps its current mapping.
- [ ] A test proves `-w '%{exitcode}'` prints `97` and a `--next` run continues with the second URL.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: step 3 prints `[97]` and `curl: (97) Recv failure: Connection was reset`, exit 97; step 5 prints `hello` and `[0]` for the second URL and exits 0. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: step 3 prints `[97]` and `curl: (97) Recv failure: Connection was reset`, exit 97. Step 5 prints `hello` and `[0]` for the second URL and exits 0.

## Log

- 2026-11-17: Created.
