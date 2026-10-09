---
id: BL-1830
title: Close GF-0037: Curl lists the smb and smbs schemes in curl -V, and the reference 8.21.0 Windows build does not
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1830 — Close GF-0037: Curl lists the smb and smbs schemes in curl -V, and the reference 8.21.0 Windows build does not

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0037 (Curl lists the smb and smbs schemes in curl -V, and the reference 8.21.0 Windows build does not), so a later gap analysis measures each of `protocols:smb`, `protocols:smbs` as `match`.

## Context

- Finding: GF-0037, filed by the gap analysis office (ADR-0433).
- Area: protocols. Severity: Medium. Introduced in: not stated upstream.
- Items: `protocols:smb`, `protocols:smbs`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/_PROTOCOLS.md` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measured: protocols:smb expected 'not listed', actual 'listed'; protocols:smbs expected 'not listed', actual 'listed' (both evidence: curl -V Protocols: line). Reproduce: powershell -NoProfile -File Gap/Tools/Measure-VersionGap.ps1 -OutDirectory $env:TEMP/gap

Suggestion, copied from the finding:

Remove smb and smbs from the Protocols: line in CurlVersionText.ProtocolsLine (Curl.Cli.UnitLibrary/CurlVersionText.cs) so curl -V matches the reference, and update the pinning test for that line in Curl.Cli.UnitTests. Explained by ADR-0189 (one known-scheme list on every platform): CommandLineProtocolSet.KnownSchemes keeps smb and smbs accepted as options, and this group does not change that set. ADR-0021 Decision 6 keeps the -V lists current with what Curl implements, so whether to drop them is a decision for the task, not an automatic removal.

## Acceptance criteria

- [ ] `protocols:smb`: Curl answers what curl 8.21.0 answers, `not listed`, so the item measures `match`.
- [ ] `protocols:smbs`: Curl answers what curl 8.21.0 answers, `not listed`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
