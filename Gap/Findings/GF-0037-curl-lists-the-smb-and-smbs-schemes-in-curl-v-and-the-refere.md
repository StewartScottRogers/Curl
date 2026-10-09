---
id: GF-0037
title: Curl lists the smb and smbs schemes in curl -V, and the reference 8.21.0 Windows build does not
area: protocols
key: protocols:smb-not-in-reference
severity: Medium
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [protocols:smb, protocols:smbs]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task:
tasks: []
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

## Log

- 2026-10-08_1640: Opened by gap-protocols.
