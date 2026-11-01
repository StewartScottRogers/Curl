---
id: GF-0087
title: Curl lists HTTP3 in Features: on Linux; the curl 8.22.0 OpenSSL reference does not
area: features
platform: linux
key: features:http3-not-in-reference
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-15_1657
regression: false
items: [features:HTTP3]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2330
tasks: [BL-2330]
---
# GF-0087 - Curl lists HTTP3 in Features: on Linux; the curl 8.22.0 OpenSSL reference does not

## Summary

Curl differs from upstream curl in features on linux: Curl lists HTTP3 in Features: on Linux; the curl 8.22.0 OpenSSL reference does not.

## Evidence

Measurement features:HTTP3: expected 'not listed', actual 'listed', state gap. Reference is curl 8.22.0 built with OpenSSL/3.0.13 and no QUIC library (no ngtcp2, quiche or OpenSSL QUIC in its version line). Reproduce on the Linux runner: curl -V | grep '^Features:' (the item's evidence).

## Suggestion

In Curl.Cli.UnitLibrary, CurlVersionText.FeaturesLine (CurlVersionText.cs, line 51), drop HTTP3 so the Linux and macOS Features: line stops listing it. Keep --http3, --http3-only and the hand-built HTTP/3 and QUIC stack working; only the line changes. Update the pinning test in Curl.Cli.UnitTests in the same change. Explained by ADR-0451: it drops HTTP3 from the Windows line only and keeps it on Linux and macOS under ADR-0144 and ADR-0021; this measurement shows the 8.22.0 OpenSSL reference does not list it, so the Linux half needs a superseding ADR. The ADR does not remove this group.

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
- 2026-10-15_0900: Filed BL-2330.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on linux.
