---
id: GF-0091
title: Curl lists the smb and smbs schemes in curl -V on Linux, and the Linux reference curl 8.22.0 with OpenSSL does not
area: protocols
platform: linux
key: protocols:smb-not-in-reference
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-15_1657
regression: false
items: [protocols:smb, protocols:smbs]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2334
tasks: [BL-2334]
---
# GF-0091 - Curl lists the smb and smbs schemes in curl -V on Linux, and the Linux reference curl 8.22.0 with OpenSSL does not

## Summary

Curl differs from upstream curl in protocols on linux: Curl lists the smb and smbs schemes in curl -V on Linux, and the Linux reference curl 8.22.0 with OpenSSL does not.

## Evidence

Measured on linux: protocols:smb expected 'not listed', actual 'listed'; protocols:smbs expected 'not listed', actual 'listed' (both evidence: curl -V Protocols: line). The measured tree's CurlVersionText.ProtocolsLine (Curl.Cli.UnitLibrary/CurlVersionText.cs, line 26) lists 'smb smbs', and Linux and macOS print ProtocolsLine unchanged. Reproduce on linux from the repository root: pwsh -NoProfile -File Gap/Tools/Measure-VersionGap.ps1 -OutDirectory /tmp/gap ; or the probe itself: curl -V | grep '^Protocols:'

## Suggestion

In Curl.Cli.UnitLibrary/CurlVersionText.cs, give Linux and macOS a third Protocols text without smb and smbs, the same way WindowsProtocolsLine does for Windows, and have CurlVersionText.Lines write it on those platforms; then update CurlVersionTextTests in Curl.Cli.UnitTests. Keep CommandLineProtocolSet.KnownSchemes and the smb and smbs handlers unchanged (ADR-0189, ADR-0200). Platform-specific: the Linux reference is built with NTLM and SMB switched on (ADR-0505), and its Protocols line still omits smb, while Ubuntu's distro curl 8.18.0 listed it (ADR-0200). ADR-0453 decided that Linux and macOS keep ProtocolsLine, but its evidence is the Windows reference, so it does not settle this group; a new ADR is needed to decide whether Linux and macOS drop smb (ADR-0021 Decision 6 governs -V lists).

## Measurements

- 2026-10-15_0751: 2 of 2 items are gaps.
- 2026-10-15_1657: 0 of 2 items are gaps.
- 2026-10-17_2225: 0 of 2 items are gaps.
- 2026-10-19_0804: 0 of 2 items are gaps.
- 2026-10-19_1329: 0 of 2 items are gaps.
- 2026-10-22_0148: 0 of 2 items are gaps.
- 2026-10-23_1637: 0 of 2 items are gaps.
- 2026-10-25_1542: 0 of 2 items are gaps.
- 2026-10-30_2103: 0 of 2 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-protocols.
- 2026-10-15_0900: Filed BL-2334.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on linux.
