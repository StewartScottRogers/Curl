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
completed: 2026-10-28
---
# BL-3984 â€” Linux: an https transfer through SslStream closes without the close_notify curl's OpenSSL build sends

## Goal

On Linux, Curl's `SslStream` path sends a TLS `close_notify` alert before it closes an https connection whenever curl 8.18.0's OpenSSL build sends one, so the server sees the same bytes on the wire.

## Context

Found by BL-3698 (CGR-169, ADR-0956). In WSL, `openssl s_server -tls1_3 -accept <p> -cert c.pem -key k.pem -ign_eof -msg` answering `HTTP/1.0 200 OK` with `Content-Length: 5` and body `reneg` on stdin, then `curl -sS -k https://localhost:<p>/`: real curl 8.18.0 (OpenSSL 3.5.5) sends `TLS 1.3, Alert [length 0002], warning close_notify` after reading the body, and the server answers with its own. A linux-x64 build of Curl (`dotnet publish Curl.Console -c Release -r linux-x64 --self-contained -p:PublishAot=false`, run in WSL) closes the TCP connection with no alert, and s_server logs `unexpected eof while reading`. Exit code and body match. Start at `SslStreamConnection`'s dispose and close path (`SslStream.ShutdownAsync`). Check the Schannel build against curl 8.22.0 Schannel on Windows too before changing it, and keep the change to the OpenSSL build if Schannel's curl sends none. BL-3698's scripts were `pha.sh` and `show.sh` in a temporary folder. Rebuild them with Record-CurlExchange.ps1 or a shell script run in WSL (no Python).

## Acceptance criteria

- [x] The repro above, run through real curl and through Curl on Linux, ends with the same TLS alerts from the client (recorded in Notes).
- [x] A focused unit test in `Curl.Networking.UnitTests` fails before the fix and passes after it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured (scripts rebuilt as a WSL shell script, s_server -tls1_3 -ign_eof -msg): real curl 8.18.0 (OpenSSL 3.5.5) in WSL and real curl 8.22.0 Schannel on Windows both send TLS 1.3, Alert [length 0002], warning close_notify after the body, and the server answers with its own. So the change applies to both builds (ADR-0959).
- Fix: SslStreamConnection.DisposeAsync calls SslStream.ShutdownAsync first, except after ShutDownSendingAsync or ClearTlsAsync (TLS already ended; after a cleared TLS the bytes are plain text) or once the server ended the transport; an IOException or InvalidOperationException sending it is ignored.
- After the fix a linux-x64 publish run in WSL: exit 0, body reneg, s_server logs <<< TLS 1.3, Alert [length 0002], warning close_notify and answers >>> ... close_notify - the same client alerts as real curl. (s_server's stdin must stay open longer than the 4 s first used: the self-contained build from /mnt/c starts slowly and the server closed first.)
- SslStreamConnectionCloseNotifyTests.DisposeAsync_AfterATransfer_SendsCloseNotify (both builds) failed with the call removed and passes with it.
- dotnet build clean; fast tests 44803 passed, 0 failed. Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.
- Choice: the hand-built TLS client's HandBuiltTlsConnection is left as it is; checking it is filed as BL-3987.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: c3955a84 5720596e 779790c0.
- 2026-10-28: Doing -> Done. SslStream https connections send close_notify on close, as curl's OpenSSL and Schannel builds do
