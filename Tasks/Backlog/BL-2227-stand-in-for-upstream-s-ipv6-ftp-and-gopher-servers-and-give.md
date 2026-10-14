---
id: BL-2227
title: Stand in for upstream's IPv6 FTP and gopher servers and give %CLIENT6IP and %CLIENT6IP-NB values so cases 252-255, 1046, 1048, 1050, 1083, 1203 and 2086 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2227 — Stand in for upstream's IPv6 FTP and gopher servers and give %CLIENT6IP and %CLIENT6IP-NB values so cases 252-255, 1046, 1048, 1050, 1083, 1203 and 2086 run

## Goal

Run the `ftp-ipv6` and `gopher-ipv6` server cases and give `%FTP6PORT`, `%GOPHER6PORT`, `%CLIENT6IP` and `%CLIENT6IP-NB` values, so cases 252, 253, 254, 255, 1046, 1048, 1050, 1083, 1203 and 2086 run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had `needs-server:ftp-ipv6` 6, `http-ipv6` 3 and `gopher-ipv6` 1, and running `Curl.Conformance.UnitTests` on 2026-10-14 names these ten by their skip reasons (`%FTP6PORT` 252-255, 1048, 1050; `%CLIENT6IP-NB` 1046, 1048, 1050; `%CLIENT6IP` 1083, 2086; `%GOPHER6PORT` 1203). The gap tool uses `Curl.Conformance.UnitLibrary`.

The harness is in memory, so no case needs IPv6 from the machine: `%HOST6IP` is `[::1]`, `%HTTP6PORT` is 8991 and a target IPv6 address gets `::1` at both ends (`SwsHttpServerConnector`; the library `CLAUDE.md` paragraph on `%HTTP6PORT`). Do the same for FTP (`FtpServerConnector`, add an IPv6 port and the EPSV/EPRT `|2|::1|port|` forms ftpserver.pl answers on IPv6) and gopher (`GopherServerConnector`). Read `runtests.pl` / `servers.pm` at curl-8_22_0 for the port numbers upstream gives them and for `%CLIENT6IP` (`[::1]`) and `%CLIENT6IP-NB` (`::1`, no brackets, used in `--interface %CLIENT6IP-NB`); `--interface` with an in-memory dial is the subject of Deferred BL-1976, so if 1046 cannot run without it, say so in Notes and leave it out rather than doing BL-1976's work. Note `UpstreamResolveCheck` already answers `%RESOLVE --ipv6` for 1083.

## Acceptance criteria

- [ ] The FTP and gopher stand-ins answer on their IPv6 ports; tests in `FtpServerConnectorTests` and `GopherServerConnectorTests` pin an IPv6 control connection, an EPSV on IPv6, and a gopher selector on the IPv6 port.
- [ ] `%FTP6PORT`, `%GOPHER6PORT`, `%CLIENT6IP` and `%CLIENT6IP-NB` have values pinned in `UpstreamCaseRunnerTests`, and screening lets `ftp-ipv6` and `gopher-ipv6` run.
- [ ] Cases 252, 253, 254, 255, 1046, 1048, 1050, 1083, 1203 and 2086 are no longer screened out (1046 as decided in Notes). Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests (no real IPv6 socket); `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

## Log

- 2026-10-14: Created.
