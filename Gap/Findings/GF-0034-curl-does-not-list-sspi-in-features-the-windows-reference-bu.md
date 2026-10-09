---
id: GF-0034
title: Curl does not list SSPI in Features; the Windows reference build does
area: features
key: features:sspi-missing
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-08_2029
regression: false
items: [features:SSPI]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1827
tasks: [BL-1827]
---
# GF-0034 - Curl does not list SSPI in Features; the Windows reference build does

## Summary

Curl differs from upstream curl in features: Curl does not list SSPI in Features; the Windows reference build does.

## Evidence

features:SSPI expected listed, actual not listed (curl -V Features: line). The reference Features: line lists SSPI; Curl's CurlVersionText.FeaturesLine does not. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

## Suggestion

In Curl.Cli.UnitLibrary/CurlVersionText.cs, add SSPI to the Windows FeaturesLine only once Curl's Windows NTLM and Negotiate path actually answers through SSPI, and pin it in CurlVersionTextTests in Curl.Cli.UnitTests. ADR-0142 says that path already runs through SSPI on Windows, so verify it before listing. ADR-0021 says the opposite (Curl calls no SSPI on any platform), so resolve that conflict in an ADR first.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 0 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-features.
- 2026-10-08_1731: Filed BL-1827.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
