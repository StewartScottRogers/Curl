---
id: GF-0031
title: Curl lists HTTP2 in Features; the Windows reference build does not
area: features
key: features:http2-not-in-reference
severity: Medium
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [features:HTTP2]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1824
tasks: [BL-1824]
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

## Log

- 2026-10-08_1640: Opened by gap-features.
- 2026-10-08_1731: Filed BL-1824.
