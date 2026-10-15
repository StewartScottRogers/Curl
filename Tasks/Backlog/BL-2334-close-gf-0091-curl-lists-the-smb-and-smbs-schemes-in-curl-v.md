---
id: BL-2334
title: Close GF-0091: Curl lists the smb and smbs schemes in curl -V on Linux, and the Linux reference curl 8.22.0 with OpenSSL does not (on linux)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2334 — Close GF-0091: Curl lists the smb and smbs schemes in curl -V on Linux, and the Linux reference curl 8.22.0 with OpenSSL does not (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0091 (Curl lists the smb and smbs schemes in curl -V on Linux, and the Linux reference curl 8.22.0 with OpenSSL does not), so a later gap analysis measures each of `protocols:smb`, `protocols:smbs` as `match`.

## Context

- Finding: GF-0091, filed by the gap analysis office (ADR-0433).
- Area: protocols. Severity: Medium. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `protocols:smb`, `protocols:smbs`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/_PROTOCOLS.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measured on linux: protocols:smb expected 'not listed', actual 'listed'; protocols:smbs expected 'not listed', actual 'listed' (both evidence: curl -V Protocols: line). The measured tree's CurlVersionText.ProtocolsLine (Curl.Cli.UnitLibrary/CurlVersionText.cs, line 26) lists 'smb smbs', and Linux and macOS print ProtocolsLine unchanged. Reproduce on linux from the repository root: pwsh -NoProfile -File Gap/Tools/Measure-VersionGap.ps1 -OutDirectory /tmp/gap ; or the probe itself: curl -V | grep '^Protocols:'

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary/CurlVersionText.cs, give Linux and macOS a third Protocols text without smb and smbs, the same way WindowsProtocolsLine does for Windows, and have CurlVersionText.Lines write it on those platforms; then update CurlVersionTextTests in Curl.Cli.UnitTests. Keep CommandLineProtocolSet.KnownSchemes and the smb and smbs handlers unchanged (ADR-0189, ADR-0200). Platform-specific: the Linux reference is built with NTLM and SMB switched on (ADR-0505), and its Protocols line still omits smb, while Ubuntu's distro curl 8.18.0 listed it (ADR-0200). ADR-0453 decided that Linux and macOS keep ProtocolsLine, but its evidence is the Windows reference, so it does not settle this group; a new ADR is needed to decide whether Linux and macOS drop smb (ADR-0021 Decision 6 governs -V lists).

## Acceptance criteria

- [ ] `protocols:smb`: Curl answers what curl 8.22.0 answers on linux, `not listed`, so the item measures `match`.
- [ ] `protocols:smbs`: Curl answers what curl 8.22.0 answers on linux, `not listed`, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
