---
id: BL-4675
title: Let the conformance HTTP/3 server negotiate an application protocol other than h3 (for DoQ's doq)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Http3Server.UnitLibrary/Http3ServerTlsSettings.cs, Curl.Conformance.Http3Server.UnitLibrary/Http3ServerClientOffer.cs, Curl.Conformance.Http3Server.UnitLibrary/Http3ServerTlsHandshake.cs, Curl.Conformance.Http3Server.UnitTests/Http3ServerTlsHandshakeTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4675 — Let the conformance HTTP/3 server negotiate an application protocol other than h3 (for DoQ's doq)

## Goal

`Curl.Conformance.Http3Server.UnitLibrary`'s `Http3ServerConnection` can be built to offer and require an ALPN
protocol other than `h3`, so a DNS over QUIC (RFC 9250) reference server can negotiate `doq` with it.

## Context

Split from BL-4474 (DnsQuery encrypted conformance). `Http3ServerTlsSettings.ApplicationProtocol` is a `const "h3"`,
read by `Http3ServerClientOffer` (line 36, refuses a ClientHello without it) and `Http3ServerTlsHandshake` (line 238,
the EncryptedExtensions ALPN). BL-4676's DoQ reference server needs `doq` there; curl's `DnsQuicTransport` offers
only `doq`. Suggested shape: an `init` property on `Http3ServerTlsSettings` defaulting to `"h3"` (keep a
`DefaultApplicationProtocol` const if other code reads the const), read from the settings instance in both places.
Every other reader of the const (`grep -rn "Http3ServerTlsSettings.ApplicationProtocol"`) must keep building.

## Acceptance criteria

- [ ] `Http3ServerTlsSettings` takes the application protocol to negotiate, defaulting to `h3`; the client offer check
      and EncryptedExtensions use the configured value.
- [ ] A test in `Curl.Conformance.Http3Server.UnitTests` completes a handshake with a client offering only `doq`
      against settings configured with `doq`, and another shows a client offering only `h3` is refused with
      `no_application_protocol` by such settings.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `Curl.Conformance.Http3Server.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and
      CRAP at most 30 per method (`Measure-CodeQuality.ps1`).

## Notes

- Filed by BL-4474's split (2026-11-03).

## Log

- 2026-11-03: Created.
