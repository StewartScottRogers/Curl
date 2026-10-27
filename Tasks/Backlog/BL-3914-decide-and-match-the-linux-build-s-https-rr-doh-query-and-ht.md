---
id: BL-3914
title: Decide and match the Linux build's HTTPS-RR DoH query and HTTPSRR feature (CGR-157 Linux half)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Cli.UnitTests/CurlVersionTextTests.cs, Curl.Conformance.UnitLibrary/UpstreamUnixFeatures.cs, Curl.Networking.UnitTests/Cgr157LinuxTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3914 — Decide and match the Linux build's HTTPS-RR DoH query and HTTPSRR feature (CGR-157 Linux half)

## Goal

On Linux and macOS, Curl's `--doh-url` traffic for an `https://` origin and its `curl -V` `HTTPSRR` feature match the platform's reference curl (the OpenSSL build), as BL-3686 made them match on Windows.

## Context

BL-3686 (ADR-0849) fixed the Windows half of third-party gap report issue CGR-157 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`): `TcpConnector.AsksHttpsRecords` defaults to off on Windows, where the Schannel build has no HTTPSRR, and on elsewhere, where `CurlVersionText.FeaturesLine` lists `HTTPSRR`. CGR-157 also reports that the Linux reference (curl 8.18.0, OpenSSL) sent only the A and AAAA DoH queries for `curl -sS -k --doh-url ... --doh-insecure https://foo.test:21805/x`, where Curl off Windows still sends a third, `_21805._https.foo.test` type 65. A lane runs only on Windows, so measure the Linux reference's `curl -V` Features line and its DoH queries (WSL with `Record-CurlExchange.ps1 -DohPort` on the Windows host's address, or curl 8.18.0 / 8.22.0 source: whether `USE_HTTPSRR` is on in a default OpenSSL build), then decide together, in an ADR amending ADR-0849: if the reference has no HTTPSRR, turn `AsksHttpsRecords` off on every platform, drop `HTTPSRR` from the Linux Features line and from `UpstreamUnixFeatures` (case 2100's `%if HTTPSRR` protocol then stops being held); otherwise record why Linux keeps it.

## Acceptance criteria

- [ ] The Linux reference curl's Features line and DoH queries for the CGR-157 repro are measured and recorded in Notes, with the source of the measurement.
- [ ] Curl off Windows sends the same DoH queries and lists `HTTPSRR` exactly when that reference does, recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [ ] A focused unit test in `Curl.Networking.UnitTests/Cgr157LinuxTests.cs` pins the off-Windows default.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3686, which fixed only the Windows half.

## Log

- 2026-10-26: Created.
