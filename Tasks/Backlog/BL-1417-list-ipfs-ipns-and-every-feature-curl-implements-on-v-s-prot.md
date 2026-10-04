---
id: BL-1417
title: List ipfs, ipns and every feature Curl implements on -V's Protocols and Features lines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-03
completed:
---
# BL-1417 — List ipfs, ipns and every feature Curl implements on -V's Protocols and Features lines

## Goal

`curl -V` announces every protocol and feature Curl actually serves, as real curl 8.21.0 does, so a script that greps `Protocols:` or `Features:` to detect a capability gets the right answer.

## Context

- Found 2026-10-03 comparing `-V` outputs. `Curl.Cli.UnitLibrary/CurlVersionText.cs` pins `ProtocolsLine` and `FeaturesLine` under ADR-0021 (Decision 4: `Protocols:` lists exactly the schemes the registered handlers serve; Decision 5: `Features:` lists exactly the features with evidence in the code; Decision 6: the task that adds one updates the line).
- **ipfs and ipns:** Curl serves `ipfs://` and `ipns://` URLs by rewriting them to a gateway (BL-210, BL-372, BL-403), as the curl tool does, and both measured curl 8.21.0 builds list `ipfs ipns` on `Protocols:`. Decision 4 drops them because no registered handler serves them. ADR-0189 rightly keeps them out of the `--proto` scheme list (curl's libcurl does not know them); that is separate and stays.
- **Features:** the installed Windows curl 8.21.0 prints `Features: alt-svc AsynchDNS HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz SPNEGO SSL SSPI threadsafe Unicode UnixSockets`; Curl prints `Features: AsynchDNS brotli GSS-API HTTP2 HTTP3 IPv6 Kerberos Largefile libz NTLM SPNEGO SSL TLS-SRP`. Curl implements at least alt-svc (`AltSvcTransferCache`), HSTS, HTTPS-proxy and Unix sockets, judging by tasks done this week, yet does not list them. The ADR-0018 mingw reference build is the Windows reference for the exact line (measure it, or read its `-V` from ADR-0018 / ADR-0021 Context).

## Acceptance criteria

- [ ] `ProtocolsLine` lists `ipfs ipns` in curl's alphabetical place, as both reference builds do.
- [ ] For each feature name curl 8.21.0 can print (`alt-svc`, `AsynchDNS`, `brotli`, `Debug`, `GSS-API`, `HSTS`, `HTTP2`, `HTTP3`, `HTTPS-proxy`, `HTTPSRR`, `IDN`, `IPv6`, `Kerberos`, `Largefile`, `libz`, `MultiSSL`, `NTLM`, `PSL`, `SPNEGO`, `SSL`, `SSPI`, `TLS-SRP`, `threadsafe`, `TrackMemory`, `Unicode`, `UnixSockets`, `zstd`, and any other in the curl-8_21_0 source's `lib/version.c` feature table), the task's Notes say whether Curl implements it, citing the code or task that shows it, and `FeaturesLine` lists exactly the implemented ones in curl's order.
- [ ] `CurlVersionTextTests` pins both lines.
- [ ] ADR-0021 gets an amendment: `Protocols:` also lists schemes the tool serves by rewriting (ipfs, ipns), as curl does; and the Features audit, with any feature deliberately left out and why.
- [ ] `--ai-help` still describes `-V` correctly (no option changes).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports no failing member; `dotnet build` is clean and the fast tests are green.

## Notes
## Log

- 2026-10-03: Created.
