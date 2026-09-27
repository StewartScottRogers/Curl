---
id: BL-369
title: Match both builds' exit 35 text when the server resets the connection mid-handshake
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-150]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
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

- [ ] Notes record each measurement: curl version, build, host and exact command line.
- [ ] Named tests in `Curl.Networking.UnitTests` pin the measured exit 35 line for a reset handshake in each build.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes, with `TlsFailureMessages` at 100% line and branch coverage.

## Notes

- Filed by BL-150.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
