---
id: GF-0029
title: Curl lists ECH in Features; the Windows reference build does not
area: features
platform: windows
key: features:ech-not-in-reference
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-10_0657
regression: false
items: [features:ECH]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1822
tasks: [BL-1822]
---
# GF-0029 - Curl lists ECH in Features; the Windows reference build does not

## Summary

Curl differs from upstream curl in features: Curl lists ECH in Features; the Windows reference build does not.

## Evidence

features:ECH expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks ECH; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

## Suggestion

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific (Lines already branches on isWindows and isMacOS) and remove ECH from the Windows line, keeping it only where the matched reference lists it. Pin each platform's line in CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0327 and ADR-0233: Curl hand-builds ECH in its TLS 1.3 client, which the Schannel reference does not offer; ADR-0021 says list only what Curl implements, so the line must also match the reference per platform.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 1 of 1 items are gaps.
- 2026-10-10_0657: 0 of 1 items are gaps.
- 2026-10-11_0936: 0 of 1 items are gaps.
- 2026-10-11_1950: 0 of 1 items are gaps.
- 2026-10-12_0951: 0 of 1 items are gaps.
- 2026-10-12_2042: 0 of 1 items are gaps.
- 2026-10-13_1406: 0 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-features.
- 2026-10-08_1731: Filed BL-1822.
- 2026-10-10_0657: Closed: run 2026-10-10_0657 measured every item as match or excluded.
