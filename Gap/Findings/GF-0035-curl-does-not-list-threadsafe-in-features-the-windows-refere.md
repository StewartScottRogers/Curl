---
id: GF-0035
title: Curl does not list threadsafe in Features; the Windows reference build does
area: features
key: features:threadsafe-missing
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-08_2029
regression: false
items: [features:threadsafe]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1828
tasks: [BL-1828]
---
# GF-0035 - Curl does not list threadsafe in Features; the Windows reference build does

## Summary

Curl differs from upstream curl in features: Curl does not list threadsafe in Features; the Windows reference build does.

## Evidence

features:threadsafe expected listed, actual not listed (curl -V Features: line). The reference Features: line lists threadsafe; Curl's CurlVersionText.FeaturesLine does not. Reproduce: powershell -NoProfile -File Gap\Tools\Measure-VersionGap.ps1 -OutDirectory $env:TEMP\gap

## Suggestion

Record in an ADR whether Curl's transfers are thread-safe in the sense curl means. If it is decided yes, add threadsafe to the FeaturesLine in Curl.Cli.UnitLibrary/CurlVersionText.cs and pin it in CurlVersionTextTests in Curl.Cli.UnitTests. ADR-0021 excludes it because Curl exposes no libcurl API for global initialisation to be thread-safe of, so the ADR must settle that first.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 0 of 1 items are gaps.
- 2026-10-10_0657: 0 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-features.
- 2026-10-08_1731: Filed BL-1828.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
