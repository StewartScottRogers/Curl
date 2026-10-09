---
id: GF-0030
title: Curl lists GSS-API in Features; the Windows reference build does not
area: features
key: features:gss-api-not-in-reference
severity: Medium
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [features:GSS-API]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1823
tasks: [BL-1823]
---
# GF-0030 - Curl lists GSS-API in Features; the Windows reference build does not

## Summary

Curl differs from upstream curl in features: Curl lists GSS-API in Features; the Windows reference build does not.

## Evidence

features:GSS-API expected not listed, actual listed (curl -V Features: line). The reference Features: line lacks GSS-API; Curl's CurlVersionText.FeaturesLine lists it. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

## Suggestion

In Curl.Cli.UnitLibrary/CurlVersionText.cs, drop GSS-API from the Windows FeaturesLine, as ADR-0021 requires for names the platform's curl does not list, and keep it on platforms whose matched reference lists it. Update CurlVersionTextTests in Curl.Cli.UnitTests. Explained by ADR-0171: Curl hand-builds the GSS-API Kerberos initiator; and by ADR-0142: on Windows Negotiate answers through SSPI, so the Windows build does not use GSS-API.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 1 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-features.
- 2026-10-08_1731: Filed BL-1823.
