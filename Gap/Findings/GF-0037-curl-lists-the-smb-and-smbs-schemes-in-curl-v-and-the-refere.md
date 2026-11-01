---
id: GF-0037
title: Curl lists the smb and smbs schemes in curl -V, and the reference 8.21.0 Windows build does not
area: protocols
platform: windows
key: protocols:smb-not-in-reference
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-10_0657
regression: false
items: [protocols:smb, protocols:smbs]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1830
tasks: [BL-1830]
---
# GF-0037 - Curl lists the smb and smbs schemes in curl -V, and the reference 8.21.0 Windows build does not

## Summary

Curl differs from upstream curl in protocols: Curl lists the smb and smbs schemes in curl -V, and the reference 8.21.0 Windows build does not.

## Evidence

Measured: protocols:smb expected 'not listed', actual 'listed'; protocols:smbs expected 'not listed', actual 'listed' (both evidence: curl -V Protocols: line). Reproduce: powershell -NoProfile -File Gap/Tools/Measure-VersionGap.ps1 -OutDirectory $env:TEMP/gap

## Suggestion

Remove smb and smbs from the Protocols: line in CurlVersionText.ProtocolsLine (Curl.Cli.UnitLibrary/CurlVersionText.cs) so curl -V matches the reference, and update the pinning test for that line in Curl.Cli.UnitTests. Explained by ADR-0189 (one known-scheme list on every platform): CommandLineProtocolSet.KnownSchemes keeps smb and smbs accepted as options, and this group does not change that set. ADR-0021 Decision 6 keeps the -V lists current with what Curl implements, so whether to drop them is a decision for the task, not an automatic removal.

## Measurements

- 2026-10-08_1640: 2 of 2 items are gaps.
- 2026-10-08_2029: 2 of 2 items are gaps.
- 2026-10-10_0657: 0 of 2 items are gaps.
- 2026-10-11_0936: 0 of 2 items are gaps.
- 2026-10-11_1950: 0 of 2 items are gaps.
- 2026-10-12_0951: 0 of 2 items are gaps.
- 2026-10-12_2042: 0 of 2 items are gaps.
- 2026-10-13_1406: 0 of 2 items are gaps.
- 2026-10-14_0904: 0 of 2 items are gaps.
- 2026-10-15_0304: 0 of 2 items are gaps.
- 2026-10-15_0543: 0 of 2 items are gaps.
- 2026-10-15_0751: 0 of 2 items are gaps.
- 2026-10-15_1657: 0 of 2 items are gaps.
- 2026-10-17_2225: 0 of 2 items are gaps.
- 2026-10-19_0804: 0 of 2 items are gaps.
- 2026-10-19_1329: 0 of 2 items are gaps.
- 2026-10-22_0148: 0 of 2 items are gaps.
- 2026-10-23_1637: 0 of 2 items are gaps.
- 2026-10-25_1542: 0 of 2 items are gaps.
- 2026-10-30_2103: 0 of 2 items are gaps.
- 2026-10-31_1828: 0 of 2 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-protocols.
- 2026-10-08_1731: Filed BL-1830.
- 2026-10-10_0657: Closed: run 2026-10-10_0657 measured every item as match or excluded.
