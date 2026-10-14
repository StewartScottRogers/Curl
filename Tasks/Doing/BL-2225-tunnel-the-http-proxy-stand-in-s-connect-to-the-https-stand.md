---
id: BL-2225
title: Tunnel the http-proxy stand-in's CONNECT to the HTTPS stand-in so cases 446, 780-783, 1849 and 3106 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2225 — Tunnel the http-proxy stand-in's CONNECT to the HTTPS stand-in so cases 446, 780-783, 1849 and 3106 run

## Goal

Relay an HTTP proxy `CONNECT` to the HTTPS stand-in's port so cases that name both `http-proxy` and `https` (446, 780, 781, 782, 783, 1849, 3106) run instead of being screened "the http-proxy stand-in tunnels no CONNECT to the https server (BL-1915)".

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; running `Curl.Conformance.UnitTests` on 2026-10-14 names these seven (they are among the 16 `needs-server:https` of the 8.22.0 gap run 2026-10-13_1406). The gap tool uses `Curl.Conformance.UnitLibrary`.

`UpstreamCaseScreening.HttpsThroughProxy` skips them. `SwsHttpServerConnector.TunnelServerForPort` already relays a `CONNECT` to `%POP3PORT`, `%IMAPPORT`, `%SMTPPORT` and `%FTPPORT` (BL-2011, BL-2035) to the stand-in for that port once the `<connect>` reply keeps the connection open; `HttpsServerConnector` is the stand-in for `%HTTPSPORT` (8989, `TlsServerConnection` over `InMemoryDuplexStream`). Extend the tunnel relay to `%HTTPSPORT` so the TLS handshake travels inside the tunnel to `HttpsServerConnector`, whose decrypted requests land in its `ReceivedBytes` for `<verify><protocol>`; the proxy's own bytes (the CONNECT) stay in `ProxyReceivedBytes` for `<verify><proxy>`. Read the seven cases to see which recorded bytes each compares.

## Acceptance criteria

- [ ] A `CONNECT <host>:%HTTPSPORT` the `<connect>` reply accepts reaches `HttpsServerConnector` through the tunnel; `SwsHttpServerConnectorTests` and `HttpsServerConnectorTests` pin it (CONNECT recorded as proxy bytes, tunnelled requests as server bytes).
- [ ] `HttpsThroughProxy` is gone from `UpstreamCaseScreening`; cases 446, 780, 781, 782, 783, 1849 and 3106 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated (the BL-1915 sentence in the HTTPS paragraph is replaced).

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
