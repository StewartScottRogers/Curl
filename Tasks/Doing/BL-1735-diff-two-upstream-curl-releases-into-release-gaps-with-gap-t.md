---
id: BL-1735
title: Diff two upstream curl releases into release gaps with Gap/Tools/Compare-UpstreamReleases.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1723, BL-1724, BL-1725, BL-1726, BL-1727, BL-1731]
touches: [Gap/Tools/Compare-UpstreamReleases.ps1, Gap/Tools/Fixtures/releases, Gap/Upstream/8.22.0, Gap/Upstream/8.21.0/behaviour.json]
requirement: none
created: 2026-10-08
completed:
---
# BL-1735 — Diff two upstream curl releases into release gaps with Gap/Tools/Compare-UpstreamReleases.ps1

## Goal

`Gap/Tools/Compare-UpstreamReleases.ps1 -From 8.21.0 -To 8.22.0` builds the newer
release's upstream inventories and diffs them against the older one's, area by area. It
writes a release diff, which `Write-GapFindings.ps1 -ReleaseDiff` (BL-1731) turns into
`scope: newest` findings tagged with the new version.

## Context

This is ADR-0433 decision 6: the baseline moves with upstream. The release diff format is
in `Gap/Instructions/Gap-Format.md` (BL-1720). The inventories come from each area tool's
`-InventoryOnly` mode: `Measure-OptionGap.ps1` (BL-1723), `Measure-VersionGap.ps1`
(BL-1724), `Measure-WriteOutGap.ps1` (BL-1725), `Measure-ExitCodeGap.ps1` (BL-1726) and
`Measure-EnvironmentGap.ps1` (BL-1727). Releases are fetched with
`Get-UpstreamRelease.ps1` (BL-1721). The weekly watcher (BL-1736) runs this script on
`ubuntu-latest` under `pwsh`, so nothing in it may be Windows-only.

**Parameters.** `-From` (default the target from `Gap/Baselines/target.json`), `-To`
(required), `-UpstreamDirectory` (default `Gap/Upstream`), `-OutFile`
(`release-<To>.json`), `-SelfTest`.

**Rules.**

- Use the `From` inventories committed under `Gap/Upstream/<From>/`, fetching and building
  them only when missing. Fetch `To` with `Get-UpstreamRelease.ps1` and write its
  inventories to `Gap/Upstream/<To>/` with each tool's `-InventoryOnly`.
- For each area: an item key in `To` but not in `From` is `added`; in `From` but not in
  `To` is `removed`; in both with different `attributes` is `changed`, with both values
  in `before` and `after`. Ignore `introducedIn` when comparing.
- Behaviour: compare `tests/data/test*` by SHA-256. A new file is `added`, a missing one
  `removed`, and a different hash `changed`. Record the hashes in the behaviour inventory
  `Gap/Upstream/<version>/behaviour.json` (number, sha256, keywords), creating it for both
  versions when missing.
- The diff names `fromVersion` and `toVersion`, and the script prints one line per area:
  added, removed, changed.

**Self-test.** Two tiny fake releases under `Gap/Tools/Fixtures/releases/` (`9.0.0` and
`9.1.0`): one option added, one removed, one with a changed `Arg:`, a new write-out
variable, a changed `strerror` text, and test files added, removed and changed. The
self-test runs with `-ArchivePath`-style local inputs and needs no network.

## Acceptance criteria

- [ ] `Gap/Tools/Compare-UpstreamReleases.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks each added, removed and changed case listed above, shows that `introducedIn` alone does not count as a change, and checks the per-area summary lines.
- [ ] Feeding the self-test's diff to `Write-GapFindings.ps1 -ReleaseDiff` in a temporary findings folder files `scope: newest` findings with `introduced-in: 9.1.0`.
- [ ] A real run `-To 8.22.0` writes `Gap/Upstream/8.22.0/*.json` and `Gap/Upstream/8.21.0/behaviour.json`. The per-area summary is recorded in this task's Notes, and those inventories are committed.
- [ ] The header help documents every parameter and rule. The script is ASCII only and uses no Windows-only API.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
