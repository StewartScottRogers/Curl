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
completed:
---
# BL-1142 — Write curl's -v line 'SSL reusing session with ALPN' when the hand-built client resumes

## Goal

When the hand-built TLS client offers a session from the run's cache, `-v` writes curl's `* SSL reusing session with ALPN '<alpn>'` line before the ClientHello, as the OpenSSL build does.

## Context

- BL-713 / ADR-0330 made the hand-built client resume from the run's `TlsSessionCache` by default. Measured 2026-10-01 (curl 8.18.0, OpenSSL 3.5.5, `openssl s_server -www`, two URLs with `-H "Connection: close"`): the second transfer prints `* SSL reusing session with ALPN '-'` before `* TLSv1.3 (OUT), TLS handshake, Client hello (1):`. curl 8.21.0's text is in `lib/vtls/openssl.c` (`ossl_connect_step1`); measure with ALPN offered too (`-` is the session's stored ALPN, none here).
- Start at `HandBuiltTlsProvider.AuthenticateAsClientAsync` where `OfferedSession` is taken; the line goes on the target's `ITransferEvents`.

## Acceptance criteria

- [ ] A `HandBuiltTlsProviderTests` test shows the line written once when a session is offered and not written without one or under `NoSessionId`, with the ALPN text curl prints, measured first.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-10-01: Created.
