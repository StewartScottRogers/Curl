---
id: BL-1440
title: Report every upstream test feature Curl has in the conformance harness and list the cases that then pass
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1439]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1440 — Report every upstream test feature Curl has in the conformance harness and list the cases that then pass

## Goal

The conformance harness reports every upstream test feature Curl actually has - so the cases that need `proxy`, `cookies`, `crypto`, `NTLM`, `Mime`, `libz`, `ipfs` and the rest run and count, instead of being skipped as "Curl lacks the feature" - and the ones that then pass are listed on the ratchet.

## Context

- `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` lists only protocol names, `SSL`, `large_file`, `local-http`, `win32` and the TLS backend; its remarks say features "Curl lacks, such as `Debug`, `cookies`, `proxy`, `libz` and `IPv6`, stay off". That is no longer true: `Curl.Console`'s own `curl -V` (2026-10-04) reports `Features: alt-svc AsynchDNS brotli ECH GSS-API HSTS HTTP2 HTTP3 HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL TLS-SRP UnixSockets zstd`, and Curl implements cookies (`Curl.Cookies.UnitLibrary`), proxies, `-F` (Mime), Digest (crypto), IPFS and `--manual`.
- The 2026-10-04 run skipped cases for: `unittest` 71, `proxy` 71, `crypto` 49, `cookies` 35, `NTLM` 27, `Mime` 24, `libz` 14, `ipfs` 11, `ftp` 8, `Debug` 6, `headers-api` 4, `HSTS` 4, `brotli` 3, `manual` 2, `Largefile` 2, `GSS-API` 2, and one each for `zstd`, `ssl-sessions`, `http/2`, `h2c`, `alt-svc`, `UnixSockets`, `Unicode`, `TrackMemory`, `PSL`, `IDN`.
- Upstream (tag `curl-8_21_0`) `tests/runtests.pl` derives the feature names from `curl -V` (e.g. `HTTP2` becomes `http/2`, and `h2c` with it) and from the build's disabled list (`cookies`, `proxy`, `Mime`, `crypto`, `ipfs`, `manual`, `headers-api`, ...); use that file as the source of each name's meaning. `unittest`, `Debug`, `TrackMemory` and `headers-api` are libcurl build or API features Curl does not have and stay off; `ftp` stays off while the harness has no FTP server (`UpstreamCaseScreening.Servers`) unless a case needs only the name.
- Depends on BL-1439, which changes the same projects and lists the cases passing before this change.

## Acceptance criteria

- [ ] `UpstreamCurlPlatform` reports each feature Curl has, by upstream's spelling, with its remarks rewritten to say which features stay off and why; a test in `Curl.Conformance.UnitTests` pins the Windows and Unix lists.
- [ ] A run of `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"` passes; every case it reports as `passes; add N` is added to `PassingUpstreamCases.txt`; the newly runnable cases that fail stay `Inconclusive`, and the ten most common first differences among them are written in Notes for follow-up tasks.
- [ ] The pass-rate table in `Curl.Conformance.UnitTests/CLAUDE.md` gains a row from that run.
- [ ] `dotnet build Curl.Conformance.UnitTests -warnaserror` is clean; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
