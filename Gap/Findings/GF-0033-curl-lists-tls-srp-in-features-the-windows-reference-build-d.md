---
id: GF-0033
title: Curl lists TLS-SRP in Features; the Windows reference build does not
area: features
key: features:tls-srp-not-in-reference
severity: Medium
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [features:TLS-SRP]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1826
tasks: [BL-1826]
---
# GF-0033 - Curl lists TLS-SRP in Features; the Windows reference build does not

## Summary

Curl differs from upstream curl in features: Curl lists TLS-SRP in Features; the Windows reference build does not.

## Evidence

features:TLS-SRP expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks TLS-SRP; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

## Suggestion

In Curl.Cli.UnitLibrary/CurlVersionText.cs, make FeaturesLine platform-specific and drop TLS-SRP from the Windows line, keeping it where the matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0328 and ADR-0229: Curl runs TLS-SRP through its hand-built TLS 1.2 client as the OpenSSL build does; the Schannel reference does not offer it.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 1 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-features.
- 2026-10-08_1731: Filed BL-1826.
