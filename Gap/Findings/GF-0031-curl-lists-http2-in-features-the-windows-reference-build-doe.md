---
id: GF-0031
title: Curl lists HTTP2 in Features; the Windows reference build does not
area: features
platform: windows
key: features:http2-not-in-reference
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-25_1542
regression: true
items: [features:HTTP2]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-3430
tasks: [BL-1824, BL-3430]
---
# GF-0031 - Curl lists HTTP2 in Features; the Windows reference build does not

## Summary

Curl differs from upstream curl in features: Curl lists HTTP2 in Features; the Windows reference build does not.

## Evidence

features:HTTP2 expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks HTTP2; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

## Suggestion

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific and drop HTTP2 from the Windows line, keeping it where the matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0141: HTTP/2 is hand-built and accepted everywhere, but offered by default only off Windows, so the Windows build does not offer it by default.

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
- 2026-10-23_1637: 1 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-features.
- 2026-10-08_1731: Filed BL-1824.
- 2026-10-10_0657: Closed: run 2026-10-10_0657 measured every item as match or excluded.
- 2026-10-23_1637: Reopened as a regression: run 2026-10-23_1637 measured features:HTTP2 as gap again on windows.
- 2026-10-23_1729: Filed BL-3430.
- 2026-10-25_1542: Closed: run 2026-10-25_1542 measured every item as match or excluded on windows.
