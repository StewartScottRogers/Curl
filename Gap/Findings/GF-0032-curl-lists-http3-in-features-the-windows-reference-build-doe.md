---
id: GF-0032
title: Curl lists HTTP3 in Features; the Windows reference build does not
area: features
key: features:http3-not-in-reference
severity: Medium
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [features:HTTP3]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1825
tasks: [BL-1825]
---
# GF-0032 - Curl lists HTTP3 in Features; the Windows reference build does not

## Summary

Curl differs from upstream curl in features: Curl lists HTTP3 in Features; the Windows reference build does not.

## Evidence

features:HTTP3 expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks HTTP3; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

## Suggestion

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific and drop HTTP3 from the Windows line, keeping it where the matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0144: HTTP/3 is hand-built over a hand-built QUIC, which the Schannel reference build does not offer; ADR-0140 notes QUIC runs TCP-only where SslStream cannot.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 1 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-features.
- 2026-10-08_1731: Filed BL-1825.
