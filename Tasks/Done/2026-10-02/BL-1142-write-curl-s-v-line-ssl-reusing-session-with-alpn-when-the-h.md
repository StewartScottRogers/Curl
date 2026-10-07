---
id: BL-1142
title: Write curl's -v line 'SSL reusing session with ALPN' when the hand-built client resumes
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1142 — Write curl's -v line 'SSL reusing session with ALPN' when the hand-built client resumes

## Goal

When the hand-built TLS client offers a session from the run's cache, `-v` writes curl's `* SSL reusing session with ALPN '<alpn>'` line before the ClientHello, as the OpenSSL build does.

## Context

- BL-713 / ADR-0330 made the hand-built client resume from the run's `TlsSessionCache` by default. Measured 2026-10-01 (curl 8.18.0, OpenSSL 3.5.5, `openssl s_server -www`, two URLs with `-H "Connection: close"`): the second transfer prints `* SSL reusing session with ALPN '-'` before `* TLSv1.3 (OUT), TLS handshake, Client hello (1):`. curl 8.21.0's text is in `lib/vtls/openssl.c` (`ossl_connect_step1`); measure with ALPN offered too (`-` is the session's stored ALPN, none here).
- Start at `HandBuiltTlsProvider.AuthenticateAsClientAsync` where `OfferedSession` is taken; the line goes on the target's `ITransferEvents`.

## Acceptance criteria

- [x] A `HandBuiltTlsProviderTests` test shows the line written once when a session is offered and not written without one or under `NoSessionId`, with the ALPN text curl prints, measured first.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Measured 2026-10-02 in WSL Ubuntu (curl 8.18.0, OpenSSL 3.5.5) against `openssl s_server -www -alpn http/1.1`, two URLs with `-H "Connection: close"`: the second transfer prints `* SSL reusing session with ALPN 'http/1.1'` before `* ALPN: curl offers h2,http/1.1` and the Client hello; without `-alpn` on the server it prints `'-'`. The text is the session's stored ALPN protocol, `-` when it has none.
- `HandBuiltTlsProvider.ReportReusedSession` writes the line right after `OfferedSession` takes a session, so it comes before the `--tls-earlydata` line, as in `openssl.c`, where the infof follows `SSL_set_session` and precedes `Curl_on_session_reuse`.
- Choice: the Schannel build writes nothing. curl's `schannel.c` reports credential-handle reuse only through `DEBUGF`, which release builds compile out.
- `--no-sessionid` takes no session (`SessionPeerKey` is null), so no line, as curl.
- Tests: `HandBuiltTlsProviderTests.ReusedSession.cs` (ALPN, no ALPN, no cached session, `NoSessionId`, Schannel build). Measure-CodeQuality: 0 failing members in Curl.Networking.UnitLibrary. No option changed, so `--ai-help` is unaffected.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v writes curl's 'SSL reusing session with ALPN' line when the hand-built client offers a cached session
