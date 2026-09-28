---
id: BL-502
title: Negotiate TLS 1.0 and 1.1 minimums and the --tls-max ceiling in SslStreamTlsProvider
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-501]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-502 — Negotiate TLS 1.0 and 1.1 minimums and the --tls-max ceiling in SslStreamTlsProvider

## Goal

The TLS provider offers exactly the protocol versions between the minimum (`-1`, `--tlsv1.0` … `--tlsv1.3`) and the `--tls-max` ceiling, for the origin and (with `--proxy-tlsv1`) the HTTPS proxy, and a version range the server or the operating system cannot meet fails with the exit code and message the platform's curl 8.21.0 build gives.

## Context

- Conformance audit 2026-09-28, row 5 (Blocker). Parsing is BL-501.
- Code: `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs`, `TlsClientOptions.cs`, `TlsMinimumVersion.cs`, `TlsFailureMessages.cs`; mapping in `Curl.Console/TlsClientOptionsMapping.cs`. The HTTPS proxy's options are separate (ADR-0095).
- ADR-0009: match the platform's build (Schannel on Windows, OpenSSL elsewhere). Windows 11 disables TLS 1.0/1.1 in the OS; Schannel's answer to `--tls-max 1.1` against a TLS 1.2-only server must be measured, as must a min above the max (`--tlsv1.3 --tls-max 1.2`).
- This task covers what `SslStream` can negotiate. Where the operating system's TLS stack refuses TLS 1.0/1.1 but an official curl build connects (curl.se's LibreSSL Windows build), BL-714 closes the gap through the hand-built TLS client (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28); pin today's `SslStream` answer here and name BL-714 in the XML docs.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls -k`: `--tls-max 1.2`, `--tls-max 1.1`, `--tlsv1.3 --tls-max 1.2`, `--tlsv1.0`, each with `-v`; stdout, stderr and exit code copied into Notes.
- [ ] Tests on the options mapping show the `SslProtocols` set offered for each min/max pair, with the obsolete members confined to one documented, suppressed place.
- [ ] Each measured failure is pinned as its exit code and message, the Windows answer under `[OSCondition(OperatingSystems.Windows)]` and the OpenSSL-build answer in its own excluded-Windows test.
- [ ] `--proxy-tlsv1` reaches the proxy's TLS options only, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
