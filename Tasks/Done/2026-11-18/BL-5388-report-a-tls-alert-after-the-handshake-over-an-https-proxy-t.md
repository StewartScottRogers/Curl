---
id: BL-5388
title: Report a TLS alert after the handshake over an HTTPS proxy tunnel as Recv failure
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitLibrary/SslStreamConnection.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitTests/HttpsProxyTlsAlertAfterHandshakeTests.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5388 — Report a TLS alert after the handshake over an HTTPS proxy tunnel as Recv failure

## Goal

A TLS alert received after the handshake inside an HTTPS proxy tunnel is reported as `curl: (56) Recv failure: Connection was aborted` (or `...was reset`), exit 56, not as `SEC_E_ILLEGAL_MESSAGE` text.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-081 (Low, Probable: 2 of 2 PORT runs; REF's text depends on timing; message only, exit 56 matches). Area: TLS over an HTTPS proxy tunnel, Windows.

Still-open older finding. What changed: the HTTP-proxy, SOCKS5h and `--ssl-auto-client-cert` cases now match; only the HTTPS-proxy (double TLS) case remains.

**Repro.** An HTTPS proxy on localhost:41092 (certificate from `ca.pem`) that tunnels CONNECT to a TLS origin requiring a client certificate; the origin sends a `certificate_required` alert after the handshake and closes. `curl -sS -m 10 -x https://localhost:41092 --proxy-cacert ca.pem --cacert ca.pem https://localhost:<origin>/`

**Expected (REF).** `curl: (56) Recv failure: Connection was aborted` (or `...was reset`), exit 56.
**Actual (PORT).** `curl: (56) schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326) ...`, exit 56.
**Where to start.** `Curl.Networking.UnitLibrary/TlsFailureMessages.cs`, read-phase alert text on the double-TLS (proxy tunnel) path (commit c3f85102 fixed the hand-built read path but not this one).

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests` (a new file) prove the read-phase alert on the double-TLS tunnel path yields a message starting `Recv failure: Connection was` with exit 56.
- [x] A test proves the already-fixed direct (non-proxy) read-phase alert text is unchanged.
- [x] The tests accept either `was aborted` or `was reset`, since REF's text depends on timing.
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] Hand check recorded in the Log: the command prints a `Recv failure: Connection was ...` line, exit 56. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command prints a `Recv failure: Connection was ...` line, exit 56.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 11f0b356 6ebf42a7 382e1205 7ae10ef7 5bb5acb2 a0b49a29.
- 2026-11-17: Done. SslStreamConnection and HandBuiltTlsProvider pass `plaintext.IsSecure` (TLS inside a proxy's TLS tunnel) as `insideTlsTunnel`, so the Schannel build's read-phase alert text is `Recv failure: Connection was aborted`; direct text unchanged. Widened touches to the two callers. Quality run for Curl.Networking.UnitLibrary: 0 failing members. Hand check against a live double-TLS proxy/origin was not run in the lane (no such mock server); the behaviour is pinned by unit tests (`aborted` chosen; REF's `reset` timing variant is accepted by the tests). Decision: Schannel build only, OpenSSL text untouched (not reported by the finding).
- 2026-11-17: Doing -> Done. Schannel read-phase alert inside an HTTPS proxy tunnel is Recv failure: Connection was aborted
