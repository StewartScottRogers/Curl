---
id: BL-3984
title: Linux: an https transfer through SslStream closes without the close_notify curl's OpenSSL build sends
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitTests/SslStreamConnectionCloseNotifyTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3984 â€” Linux: an https transfer through SslStream closes without the close_notify curl's OpenSSL build sends

## Goal

On Linux, Curl's `SslStream` path sends a TLS `close_notify` alert before it closes an https connection whenever curl 8.18.0's OpenSSL build sends one, so the server sees the same bytes on the wire.

## Context

Found by BL-3698 (CGR-169, ADR-0956). In WSL, `openssl s_server -tls1_3 -accept <p> -cert c.pem -key k.pem -ign_eof -msg` answering `HTTP/1.0 200 OK` with `Content-Length: 5` and body `reneg` on stdin, then `curl -sS -k https://localhost:<p>/`: real curl 8.18.0 (OpenSSL 3.5.5) sends `TLS 1.3, Alert [length 0002], warning close_notify` after reading the body, and the server answers with its own. A linux-x64 build of Curl (`dotnet publish Curl.Console -c Release -r linux-x64 --self-contained -p:PublishAot=false`, run in WSL) closes the TCP connection with no alert, and s_server logs `unexpected eof while reading`. Exit code and body match. Start at `SslStreamConnection`'s dispose and close path (`SslStream.ShutdownAsync`). Check the Schannel build against curl 8.22.0 Schannel on Windows too before changing it, and keep the change to the OpenSSL build if Schannel's curl sends none. BL-3698's scripts were `pha.sh` and `show.sh` in a temporary folder. Rebuild them with Record-CurlExchange.ps1 or a shell script run in WSL (no Python).

## Acceptance criteria

- [ ] The repro above, run through real curl and through Curl on Linux, ends with the same TLS alerts from the client (recorded in Notes).
- [ ] A focused unit test in `Curl.Networking.UnitTests` fails before the fix and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: c3955a84 5720596e 779790c0.
