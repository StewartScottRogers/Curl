---
id: GF-0088
title: Curl does not list SSLS-EXPORT in Features: on Linux; the curl 8.22.0 OpenSSL reference does
area: features
platform: linux
key: features:ssls-export-missing
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-15_1657
regression: false
items: [features:SSLS-EXPORT]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2331
tasks: [BL-2331]
---
# GF-0088 - Curl does not list SSLS-EXPORT in Features: on Linux; the curl 8.22.0 OpenSSL reference does

## Summary

Curl differs from upstream curl in features on linux: Curl does not list SSLS-EXPORT in Features: on Linux; the curl 8.22.0 OpenSSL reference does.

## Evidence

Measurement features:SSLS-EXPORT: expected 'listed', actual 'not listed', state gap. Reference is curl 8.22.0 built with OpenSSL/3.0.13. Reproduce on the Linux runner: curl -V | grep '^Features:' (the item's evidence). The upstream manual, under SSLS-EXPORT, says the build supports TLS session export/import, like with --ssl-sessions.

## Suggestion

In Curl.Cli.UnitLibrary, CurlVersionText.FeaturesLine (CurlVersionText.cs, line 51), add SSLS-EXPORT after SSL, in curl's case-insensitive order, so it reads SSL, SSLS-EXPORT, threadsafe. Do not add it to WindowsFeaturesLine: CommandLineOptionTable marks --ssl-sessions RefusedBySchannelBuild (ADR-0397), so the Windows build does not serve it. Before listing it, confirm --ssl-sessions round-trips TLS session tickets on Linux and macOS through the hand-built client (ADR-0319) and add a test in Curl.Cli.UnitTests. Explained by ADR-0021's SSLS-EXPORT row, which says no measured build prints it: the 8.22.0 OpenSSL reference does, so that row is out of date on Linux and needs an amending ADR.

## Measurements

- 2026-10-15_0751: 1 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-features.
- 2026-10-15_0900: Filed BL-2331.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on linux.
