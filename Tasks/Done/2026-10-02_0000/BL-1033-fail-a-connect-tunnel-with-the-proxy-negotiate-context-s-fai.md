---
id: BL-1033
title: Fail a CONNECT tunnel with the proxy Negotiate context's failure as curl's error message
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-604]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-02
---
# BL-1033 — Fail a CONNECT tunnel with the proxy Negotiate context's failure as curl's error message

## Goal

A CONNECT tunnel refused with `407` after a `--proxy-negotiate` context failed ends with curl 8.21.0's error message, the context's failure line (`curl: (7) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package` on Windows), not `CONNECT tunnel failed, response 407`.

## Context

- Measured in BL-604's Notes (curl 8.21.0, mingw, SSPI, 2026-09-30): `curl -s -S -v -p -x http://127.0.0.1:18606 --proxy-negotiate -U : http://example.test/` against a proxy answering the CONNECT `407` with `Proxy-Authenticate: Negotiate` writes `* CONNECT tunnel failed, response 407` under `-v` but ends `curl: (7) InitializeSecurityContext failed: ...`, exit 7, because libcurl's error buffer keeps the first `failf` of the transfer. Measure the OpenSSL/GSS-API build too (WSL) before pinning its line.
- `TcpConnector.TunnelFailure` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) makes the message; the failure lines come from `NegotiateFailureLines` through `HttpAuthRequest.Events` in `NegotiateHttpAuthenticator`. ADR-0270 records the gap.
- The forward proxy (HTTP handler) ends such a transfer with exit 0 and the 407, so only the tunnel is affected.

## Acceptance criteria

- [x] The Windows case above is measured with `Record-CurlExchange.ps1 -Script`, and a non-Windows build's too; request bytes, stderr and exit code in Notes.
- [x] A `Curl.Networking.UnitTests` test per platform (`OSCondition`) pins the tunnel's exit 7 and the failure line as `ConnectResult.ErrorMessage`, with a fake token source answering `NoCredentials`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-10-02 with `Record-CurlExchange.ps1 -Script` (steps: `read`; `send HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Negotiate\r\nContent-Length: 4\r\n\r\ndeny`; `read`; `close`), `-s -S -v -p -x http://<proxy> --proxy-negotiate -U : http://example.test/`:
  - curl 8.21.0 (mingw, Schannel, SSPI): request `CONNECT example.test:80 HTTP/1.1` / `Host: example.test:80` / `User-Agent: curl/8.21.0` / `Proxy-Connection: Keep-Alive` (no `Proxy-Authorization`), one CONNECT. `-v`: `InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`, `Proxy auth using Negotiate with user ''`, `Establishing HTTP proxy tunnel to example.test:80`, the CONNECT, `< HTTP/1.1 407 ...`, `< Proxy-Authenticate: Negotiate`, the failure line again, `< Content-Length: 4`, `* CONNECT tunnel failed, response 407`, `* closing connection #0`; stderr ends `curl: (7) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`; exit 7.
  - curl 8.18.0 (WSL Ubuntu, OpenSSL, MIT krb5 GSS-API; `-Curl wsl.exe`, `-ListenAddress` the WSL gateway): the same CONNECT with `User-Agent: curl/8.18.0`; `-v` `gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ` before `Proxy auth using Negotiate with user ''` and after the `Proxy-Authenticate` line; stderr ends `curl: (7) CONNECT tunnel failed, response 407`; exit 7. GSS-API's line is `infof`, SSPI's is `failf`, so only the SSPI build's error buffer holds it.
- Decision (ADR-0348): `TcpConnector` gives the proxy authenticator a new `SspiFailureRecordingTransferEvents` around the target's events (so the failure lines now also reach `-v` for a tunnel, as curl writes them), and a refused tunnel's message is the first `InitializeSecurityContext failed: ...` line when there is one. No contract change: the SSPI prefix is curl's own wording. CONNECT-UDP keeps `NoTransferEvents`.
- Tests: `TcpConnectorTests.ConnectAsync_WithProxyNegotiate_WhenTheContextHasNoCredentials_FailsWithTheSspiFailureLine` (Windows) and `..._FailsWithThe407` (other platforms; skipped on this Windows lane, CI runs it), using the platform's `NegotiateHttpAuthenticator` wording and a `ScriptedTokenSource` answering `NoCredentials`; `SspiFailureRecordingTransferEventsTests` (3). `CountingTransferEvents` in `HandshakeCapturingTransferEventsTests` is now `internal` so both test classes share it.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green in all 33 test assemblies (Networking 2559 passed, 26 skipped); `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100% line, 100% branch, 0 failing members. `Curl.Authentication.UnitLibrary` was not changed.

## Log

- 2026-09-30: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A CONNECT tunnel refused with 407 after a --proxy-negotiate context failed ends with the SSPI failure line on Windows (exit 7), the 407 message elsewhere, and the failure lines reach -v (ADR-0348)
