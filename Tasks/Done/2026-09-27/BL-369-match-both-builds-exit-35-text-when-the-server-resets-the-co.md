---
id: BL-369
title: Match both builds' exit 35 text when the server resets the connection mid-handshake
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-150]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-369 — Match both builds' exit 35 text when the server resets the connection mid-handshake

## Goal

When the server resets the TCP connection during the TLS handshake (rather than closing it cleanly), `SslStreamTlsProvider` prints the text each reference build prints.

## Context

- BL-150 pinned the clean close (FIN after reading the ClientHello). A server that closes without reading the ClientHello makes Windows send a reset instead. Measured on 2026-09-27 with a Python listener that accepts and closes at once:
  - curl 8.21.0 Schannel (`/mingw64/bin/curl -sS` and System32 `curl.exe`): exit 35 `Recv failure: Connection was aborted`.
  - curl 8.21.0 OpenSSL (`curlimages/curl:8.21.0` in Docker, reaching the Windows host): exit 35 `TLS connect error: error:0A000126:SSL routines::unexpected eof while reading` - but through Docker Desktop's forwarding the client probably saw a clean close, not the reset, so re-measure on a real Linux host.
- Today, in the Schannel build a `SocketException` (a `Win32Exception`) inside the handshake failure is reported as `schannel: next InitializeSecurityContext failed: Unknown error (0x00002746) - ...`, which no curl prints. `TlsFailureMessages.SchannelSslConnectError` should skip socket errors. In the OpenSSL build the socket error's own message stands in.
- Measure curl's text for WSAECONNRESET and WSAECONNABORTED on Windows and ECONNRESET on Linux before pinning.

## Acceptance criteria

- [x] Notes record each measurement: curl version, build, host and exact command line.
- [x] Named tests in `Curl.Networking.UnitTests` pin the measured exit 35 line for a reset handshake in each build.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes, with `TlsFailureMessages` at 100% line and branch coverage.

## Notes

- Filed by BL-150.
- Added `Record-CurlExchange.ps1` to `touches` (new `-Reset` switch: each accepted connection closes with a zero linger time, so Windows sends a RST) and `Documentation/Planning/Decisions` for ADR-0088. No task in Doing names either.
- Measured 2026-09-27, Windows 11 Pro 10.0.26200, `Record-CurlExchange.ps1 -Port 18369 -Reset -Curl <curl> -CurlArgs '-sS','https://127.0.0.1:18369/'`:
  - curl 8.21.0 (x86_64-w64-mingw32) Schannel, `C:\Program Files\Git\mingw64\bin\curl.exe`: exit 35 `curl: (35) Recv failure: Connection was reset` once, `Recv failure: Connection was aborted` twice (Windows reports WSAECONNRESET or WSAECONNABORTED by timing).
  - curl 8.21.0 (Windows) Schannel, `C:\Windows\System32\curl.exe`: exit 35 `Recv failure: Connection was aborted`, three of three.
- Measured 2026-09-27 on a real Linux kernel (Docker Desktop's VM), client and server in one container so no forwarding sits between them: `docker run --rm --entrypoint sh curlimages/curl:8.21.0 -c '... nc -l -p 4433 -e /tmp/s & sleep 1; curl -sS https://127.0.0.1:4433/'`, where `/tmp/s` is `sleep 1`, so the server exits without reading the ClientHello and Linux resets. curl 8.21.0 (x86_64-pc-linux-musl) OpenSSL/3.5.7: exit 35 `curl: (35) Recv failure: Connection reset by peer`, three of three. Record-CurlExchange.ps1 cannot run there (no PowerShell in the image, and WSL here is NAT-mode, so it cannot reach the Windows loopback listener).
- Decision (ADR-0088, decided by Claude under Stewart's delegation): any `SocketException` in the handshake failure is `Recv failure: <text>`, checked first in both builds. Text by `SocketError`: Schannel `Connection was reset` / `Connection was aborted`, OpenSSL `Connection reset by peer`; any other socket error uses its own .NET message.
- Tests: `TlsFailureMessagesTests.SchannelSslConnectError_WhenTheServerResetsMidHandshake_IsTheMeasuredRecvFailureLine`, `OpenSslSslConnectError_WhenTheServerResetsMidHandshake_IsTheMeasuredRecvFailureLine`, `SslConnectError_WithASocketErrorCurlIsNotMeasuredFor_UsesTheSocketErrorsOwnMessage`, and `SslStreamTlsProviderTests.AuthenticateAsClientAsync_WhenTheServerResetsMidHandshake_ReportsTheMeasuredLine` over the new `Fakes/ResettingConnection`. `OpenSslSslConnectError_WithNoOpenSslErrorStringAndNoEndOfStream_UsesTheInnermostMessage` now uses a non-socket innermost exception.
- Verified: `dotnet build Curl.Networking.UnitTests -warnaserror` clean; fast tests 674 passed, 6 skipped; `TlsFailureMessages` line-rate 1, branch-rate 1 (cobertura).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A server reset mid-handshake is exit 35 Recv failure with each build's measured text
