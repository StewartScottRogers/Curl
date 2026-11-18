---
id: BL-5430
title: Report a SOCKS5 GSS-API send failure as CURLPX_GSSAPI with curl's per-step message
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-5430 — Report a SOCKS5 GSS-API send failure as CURLPX_GSSAPI with curl's per-step message

## Goal

When a write fails during SOCKS5 GSS-API negotiation, Curl reports what curl reports: `CURLPX_GSSAPI`, curl's message for the step whose send failed, and the GSS-API verbose lines under `-v`.

## Context

The code-reviewer and silent-failure-hunter found this in the shift-end review of f1af53a37c...1257be9874 (ADR-0710).

The BL-5423 commit 0a60fe6b7e changed `Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:180-186`. `SendMessageAsync` now passes `CurlProxyCode.SendAuthentication` and the fixed text "Failed to send GSS-API authentication request." to `SocksProxyTunnel.SendAsync`, and that call throws `SocksSendFailedException` when the write fails. Three things are wrong with that:

1. **Wrong proxy code.** Every other GSS-API failure in this file returns `CurlProxyCode.Gssapi`. Upstream `socks.c` maps any failure of `Curl_SOCKS5_gssapi_negotiate` to `CURLPX_GSSAPI`, so `%{proxy_error}` differs from curl. The exit code is 97 either way.
2. **Wrong message.** `SendMessageAsync` serves both the authentication message (line 104) and the protection-level message (line 161). Upstream `socks_gssapi.c` words the encryption sends differently, and the SSPI build (`options.UsesSspiTexts`) says "SSPI" rather than "GSS-API". The text belongs in `texts`.
3. **Missing verbose lines.** The thrown exception skips the `texts.VerboseLines` loop in `RunAsync`, which every other GSS-API failure goes through.

Fix sketch: catch the send failure in `SendMessageAsync` (or at its two callers) and return `SocksProxyTunnel.Failed(CurlProxyCode.Gssapi, <the step's send-failed text>)`. Measure the texts against real curl on both builds before pinning them.

## Acceptance criteria

- [ ] A unit test in `Curl.Networking.UnitTests` resets the connection during the GSS-API authentication send. It gets `CurlProxyCode.Gssapi`, curl's authentication-send message and the verbose lines.
- [ ] A unit test resets the connection during the protection-level send and gets curl's encryption-send message for that step.
- [ ] The SSPI-text build's wording is pinned in its own test, measured against real curl.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created.
