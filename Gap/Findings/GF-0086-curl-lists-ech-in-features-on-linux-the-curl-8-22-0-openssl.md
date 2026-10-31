---
id: GF-0086
title: Curl lists ECH in Features: on Linux; the curl 8.22.0 OpenSSL reference does not
area: features
platform: linux
key: features:ech-not-in-reference
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-15_1657
regression: false
items: [features:ECH]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2329
tasks: [BL-2329]
---
# GF-0086 - Curl lists ECH in Features: on Linux; the curl 8.22.0 OpenSSL reference does not

## Summary

Curl differs from upstream curl in features on linux: Curl lists ECH in Features: on Linux; the curl 8.22.0 OpenSSL reference does not.

## Evidence

Measurement features:ECH: expected 'not listed', actual 'listed', state gap. Reference is curl 8.22.0 (x86_64-pc-linux-gnu) built with OpenSSL/3.0.13, per the measurement's reference field. Reproduce on the Linux runner: curl -V | grep '^Features:' (the item's evidence). ECH is absent from the reference line and present in Curl's FeaturesLine.

## Suggestion

In Curl.Cli.UnitLibrary, CurlVersionText.FeaturesLine (CurlVersionText.cs, line 51), drop ECH so the Linux and macOS Features: line stops listing it. Keep --ech and the hand-built EchClientHello and EchConfigList in Curl.Tls.UnitLibrary working; only the line changes. FeaturesLine is shared by Linux and macOS, so the macOS measurement must agree before the change lands. Update the pinning test in Curl.Cli.UnitTests in the same change (ADR-0021 Decision 6). explained by ADR-0448: that ADR drops ECH from the Windows line only and keeps it on Linux and macOS, on the premise that the OpenSSL build lists it; this measurement shows the 8.22.0 OpenSSL reference does not, so the Linux half needs a superseding ADR. The ADR does not remove this group.

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

## Log

- 2026-10-15_0751: Opened by gap-features.
- 2026-10-15_0900: Filed BL-2329.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on linux.
