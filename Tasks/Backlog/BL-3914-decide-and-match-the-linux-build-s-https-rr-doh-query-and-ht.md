---
id: BL-3914
title: Decide and match the Linux build's HTTPS-RR DoH query and HTTPSRR feature (CGR-157 Linux half)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3946]
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Cli.UnitTests/CurlVersionTextTests.cs, Curl.Conformance.UnitLibrary/UpstreamUnixFeatures.cs, Curl.Networking.UnitTests/Cgr157LinuxTests.cs, Curl.Networking.UnitTests/Cgr157Tests.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs, Curl.Conformance.UnitLibrary/UpstreamCurlVersionLineFeatures.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3914 — Decide and match the Linux build's HTTPS-RR DoH query and HTTPSRR feature (CGR-157 Linux half)

## Goal

On Linux and macOS, Curl's `--doh-url` traffic for an `https://` origin and its `curl -V` `HTTPSRR` feature match the platform's reference curl (the OpenSSL build), as BL-3686 made them match on Windows.

## Context

BL-3686 (ADR-0856) fixed the Windows half of third-party gap report issue CGR-157 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`): `TcpConnector.AsksHttpsRecords` defaults to off on Windows, where the Schannel build has no HTTPSRR, and on elsewhere, where `CurlVersionText.FeaturesLine` lists `HTTPSRR`. CGR-157 also reports that the Linux reference (curl 8.18.0, OpenSSL) sent only the A and AAAA DoH queries for `curl -sS -k --doh-url ... --doh-insecure https://foo.test:21805/x`, where Curl off Windows still sends a third, `_21805._https.foo.test` type 65. A lane runs only on Windows, so measure the Linux reference's `curl -V` Features line and its DoH queries (WSL with `Record-CurlExchange.ps1 -DohPort` on the Windows host's address, or curl 8.18.0 / 8.22.0 source: whether `USE_HTTPSRR` is on in a default OpenSSL build), then decide together, in an ADR amending ADR-0856: if the reference has no HTTPSRR, turn `AsksHttpsRecords` off on every platform, drop `HTTPSRR` from the Linux Features line and from `UpstreamUnixFeatures` (case 2100's `%if HTTPSRR` protocol then stops being held); otherwise record why Linux keeps it.

## Acceptance criteria

- [ ] The Linux reference curl's Features line and DoH queries for the CGR-157 repro are measured and recorded in Notes, with the source of the measurement.
- [ ] Curl off Windows sends the same DoH queries and lists `HTTPSRR` exactly when that reference does, recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [ ] A focused unit test in `Curl.Networking.UnitTests/Cgr157LinuxTests.cs` pins the off-Windows default.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3686, which fixed only the Windows half.
- 2026-10-28 (lane 7) measurement: WSL Ubuntu `curl -V` = curl 8.18.0 libcurl/8.18.0 OpenSSL/3.5.5 (8.18.0-1ubuntu2.8). Features: alt-svc AsynchDNS brotli GSS-API HSTS HTTP2 HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL threadsafe TLS-SRP UnixSockets zstd - no HTTPSRR (and, outside this task, no SSLS-EXPORT). DoH queries for the CGR-157 repro: the gap report's own Linux measurement (CGR-157 "Expected", REF 8.18 Linux) sends only the A and AAAA POSTs. So the Linux reference has no HTTPSRR.
- Decision to take (ADR amending ADR-0856): `TcpConnector.AsksHttpsRecords` defaults to false on every platform; drop `HTTPSRR` from `CurlVersionText.FeaturesLine` (and its test constant, doc comments), from `UpstreamUnixFeatures.Names`, from the `UpstreamCurlPlatformTests.Unix_...` superset list and the `UpstreamCurlVersionLineFeatures` doc; make `Cgr157Tests`' Windows-only test platform-neutral and replace its off-Windows test with `Cgr157LinuxTests` pinning no HTTPS-RR query off Windows. Tests that need the HTTPS-RR path already set `AsksHttpsRecords = true`.
- Touches widened for `Cgr157Tests.cs` (its off-Windows test pins the old default), `UpstreamCurlPlatformTests.cs` (its Unix superset names HTTPSRR) and `UpstreamCurlVersionLineFeatures.cs` (doc names HTTPSRR as Linux-only). BL-3946 holds the whole of Curl.Conformance.UnitTests and UnitLibrary, so this task waits on it (rule 3); no code was edited in this run.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 90befad2 74ba9b3c 168cb378 779790c0.
- 2026-10-28: Doing -> Backlog. Waits on BL-3946, which holds Curl.Conformance.UnitTests and Curl.Conformance.UnitLibrary (UpstreamCurlPlatformTests.cs, UpstreamUnixFeatures.cs); measurement and decision recorded in Notes
