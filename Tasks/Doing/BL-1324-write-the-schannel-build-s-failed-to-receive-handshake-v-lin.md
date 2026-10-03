---
id: BL-1324
title: Write the Schannel build's 'failed to receive handshake' -v line before a refused TLS version range's exit 35
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1323]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1324 — Write the Schannel build's 'failed to receive handshake' -v line before a refused TLS version range's exit 35

## Goal

When the Schannel build's handshake fails with `schannel: failed to receive handshake, SSL/TLS connection failed` (exit 35), the TLS provider reports that text as a `-v` info line after `* ALPN: curl offers http/1.1`, as curl 8.21.0's `failf` does, so `--tlsv1.3` against a TLS 1.2-only server prints it before `* closing connection #0` and `curl: (35) ...`.

## Context

- Today the text is `TlsFailureMessages.SchannelHandshakeNotReceived` (`Curl.Networking.UnitLibrary/TlsFailureMessages.cs` line 51), returned by `SchannelSslConnectError` and `SchannelHandBuiltHandshakeFailure` as the exit 35 message (`SslStreamTlsProvider.SslConnectError` near line 721, `HandBuiltTlsProvider` near line 768), but it never reaches the events, so `-v` shows nothing between the ALPN offer and `curl: (35)`.
- Measured 2026-10-03 with the installed curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -Tls -CurlArgs '-v','-k','--tlsv1.3','https://127.0.0.1:PORT/'` (the recorder serves TLS 1.2 only), stderr exactly:
  ```
  *   Trying 127.0.0.1:PORT...
  * schannel: disabled automatic use of client certificate
  * schannel: using IP address, SNI is not supported by OS.
  * ALPN: curl offers http/1.1
  * schannel: failed to receive handshake, SSL/TLS connection failed
  * closing connection #0
  curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed
  ```
  exit 35.
- BL-1323 adds the same kind of line for exit 60's `SEC_E_UNTRUSTED_ROOT`; reuse its mechanism. Any other Schannel exit 35 message (a named security status, `Recv failure: ...`) is also a `failf` in curl, so report the message whatever `SchannelSslConnectError` returns, not only this one text, unless a measurement in the Notes shows otherwise.
- Only the Schannel build: the OpenSSL build's exit 35 lines are BL-1178's area and are not changed here.

## Acceptance criteria

- [ ] A test in `Curl.Networking.UnitTests` drives `SslStreamTlsProvider` as the Schannel build into the `failed to receive handshake` failure (as the existing exit 35 tests do, e.g. a transport that closes during the handshake) and asserts the info line `schannel: failed to receive handshake, SSL/TLS connection failed` is reported once, after the failed handshake event, with the result unchanged (exit 35, same message).
- [ ] A test pins the same for `HandBuiltTlsProvider` as the Schannel build when the server closes during the handshake (`TlsHandshakeFailureOrigin.TransportClosed`).
- [ ] A test pins that a Schannel exit 35 with a named security status reports that message as its info line too.
- [ ] A test pins that the OpenSSL build reports no new line for the same failures.
- [ ] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
