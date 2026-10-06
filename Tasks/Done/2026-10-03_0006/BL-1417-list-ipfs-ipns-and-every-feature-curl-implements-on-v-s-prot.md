---
id: BL-1417
title: List ipfs, ipns and every feature Curl implements on -V's Protocols and Features lines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1417 — List ipfs, ipns and every feature Curl implements on -V's Protocols and Features lines

## Goal

`curl -V` announces every protocol and feature Curl actually serves, as real curl 8.21.0 does, so a script that greps `Protocols:` or `Features:` to detect a capability gets the right answer.

## Context

- Found 2026-10-03 comparing `-V` outputs. `Curl.Cli.UnitLibrary/CurlVersionText.cs` pins `ProtocolsLine` and `FeaturesLine` under ADR-0021 (Decision 4: `Protocols:` lists exactly the schemes the registered handlers serve; Decision 5: `Features:` lists exactly the features with evidence in the code; Decision 6: the task that adds one updates the line).
- **ipfs and ipns:** Curl serves `ipfs://` and `ipns://` URLs by rewriting them to a gateway (BL-210, BL-372, BL-403), as the curl tool does, and both measured curl 8.21.0 builds list `ipfs ipns` on `Protocols:`. Decision 4 drops them because no registered handler serves them. ADR-0189 rightly keeps them out of the `--proto` scheme list (curl's libcurl does not know them); that is separate and stays.
- **Features:** the installed Windows curl 8.21.0 prints `Features: alt-svc AsynchDNS HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz SPNEGO SSL SSPI threadsafe Unicode UnixSockets`; Curl prints `Features: AsynchDNS brotli GSS-API HTTP2 HTTP3 IPv6 Kerberos Largefile libz NTLM SPNEGO SSL TLS-SRP`. Curl implements at least alt-svc (`AltSvcTransferCache`), HSTS, HTTPS-proxy and Unix sockets, judging by tasks done this week, yet does not list them. The ADR-0018 mingw reference build is the Windows reference for the exact line (measure it, or read its `-V` from ADR-0018 / ADR-0021 Context).

## Acceptance criteria

- [x] `ProtocolsLine` lists `ipfs ipns` in curl's alphabetical place, as both reference builds do.
- [x] For each feature name curl 8.21.0 can print (`alt-svc`, `AsynchDNS`, `brotli`, `Debug`, `GSS-API`, `HSTS`, `HTTP2`, `HTTP3`, `HTTPS-proxy`, `HTTPSRR`, `IDN`, `IPv6`, `Kerberos`, `Largefile`, `libz`, `MultiSSL`, `NTLM`, `PSL`, `SPNEGO`, `SSL`, `SSPI`, `TLS-SRP`, `threadsafe`, `TrackMemory`, `Unicode`, `UnixSockets`, `zstd`, and any other in the curl-8_21_0 source's `lib/version.c` feature table), the task's Notes say whether Curl implements it, citing the code or task that shows it, and `FeaturesLine` lists exactly the implemented ones in curl's order.
- [x] `CurlVersionTextTests` pins both lines.
- [x] ADR-0021 gets an amendment: `Protocols:` also lists schemes the tool serves by rewriting (ipfs, ipns), as curl does; and the Features audit, with any feature deliberately left out and why.
- [x] `--ai-help` still describes `-V` correctly (no option changes).
- [x] `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports no failing member; `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-03 (lane 7): measured the installed mingw curl 8.21.0 `-V` today; its line is now
  `Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd`
  (no `Unicode`, unlike the Context's quote), so the line stays platform-invariant.
- Features audit: the full per-feature table with evidence is ADR-0021's amendment of 2026-10-03.
  Listed: alt-svc (AltSvcTransferCache), AsynchDNS, brotli/libz/zstd (HttpContentDecoder), ECH (Curl.Tls Ech*,
  DohDnsResolver), GSS-API, HSTS (HstsTransferPolicy), HTTP2, HTTP3, HTTPS-proxy (TcpConnector), HTTPSRR
  (DohDnsResolver.ResolveHttpsRecordAsync, BL-707), IDN (CurlUrlHost IdnMapping), IPv6, Kerberos, Largefile, NTLM,
  PSL (PublicSuffixList), SPNEGO, SSL, TLS-SRP, UnixSockets (UnixSocketAddress). Left out: asyn-rr, CharConv,
  Debug, gsasl (SCRAM is `not builtin`), MultiSSL, SSLS-EXPORT, SSPI (Kerberos/NTLM are hand-built, no SSPI),
  threadsafe (no libcurl API), TrackMemory, Unicode (the mingw reference does not print it).
- Touches widened to Curl.Console.UnitTests: `CurlCompositionSshTests` pins the whole `Protocols:` line and
  `CurlCompositionLdapTests` a substring next to `imaps`; both updated (two lines). BL-1396 in Doing also touches
  Curl.Console.UnitTests, so by the lane rule the task goes back to Backlog; the code is left uncommitted for the
  shift's stash. Done so far and green: build, Curl.Cli.UnitTests (3764 passed), Curl.Console.UnitTests
  CurlComposition* (516 passed). Left: re-run the fast tests, `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`
  (constants and doc comments only; no new branches), then Done.
- 2026-10-03 (lane 4): resumed from stash 56282083 (applied as a diff, cleanly); BL-1396 is Done, so
  Curl.Console.UnitTests no longer overlaps. Build clean, every fast test project green (Curl.Cli.UnitTests
  3764 passed, Curl.Console.UnitTests 2550 passed). `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`:
  99.98% line / 99.89% branch, worst CRAP 10; the three failing members (`ArgumentReader.PeekNext`,
  `CommandLineParser.RefuseUnlistedLetter`, `AccountHomeDirectory`'s lambda) predate this task, none is in its
  diff, and BL-1421 in Backlog already covers all three, so they are left to it.
## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Backlog. Needs Curl.Console.UnitTests (two -V Protocols assertions), which BL-1396 in Doing touches; code and ADR amendment done and green, left uncommitted for the stash
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. curl -V lists ipfs ipns on Protocols and every implemented feature on Features, in curl's order
