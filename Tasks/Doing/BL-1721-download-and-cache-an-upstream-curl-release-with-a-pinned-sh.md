---
id: BL-1721
title: Download and cache an upstream curl release with a pinned SHA-256 in Gap/Tools/Get-UpstreamRelease.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1720]
touches: [Gap/Tools/Get-UpstreamRelease.ps1, Gap/Tools/Fixtures/release, Gap/Baselines]
requirement: none
created: 2026-10-08
completed:
---
# BL-1721 — Download and cache an upstream curl release with a pinned SHA-256 in Gap/Tools/Get-UpstreamRelease.ps1

## Goal

`Gap/Tools/Get-UpstreamRelease.ps1 -Version 8.21.0` leaves the release's `docs/`,
`tests/data/`, `lib/strerror.c` and `COPYING` in a cache outside the repository, checked
against a SHA-256 pinned in `Gap/Baselines/curl-8.21.0.json`, and prints the cache path.

## Context

This is ADR-0433 decision 1: the yardstick is upstream's release tarball. The formats of
`Gap/Baselines/curl-<version>.json` and `Gap/Baselines/target.json` are in
`Gap/Instructions/Gap-Format.md` (BL-1720).

The pattern to follow is
`Curl.Conformance.UnitTests/UpstreamTestData/Update-UpstreamTestData.ps1`. It downloads a
tag archive with `Invoke-WebRequest` and extracts named members with Windows' own
`System32\tar.exe`. A Git Bash GNU tar earlier on PATH reads `C:\...` as a remote host, so
that script prefers System32's, and plain `tar` off Windows. Reuse that approach. Do not
call that script, because it writes into the test project.

Behaviour:

- Parameters: `-Version` (default: `version` from `Gap/Baselines/target.json`),
  `-CacheRoot` (default `$env:LOCALAPPDATA\Curl\gap\upstream` on Windows, otherwise
  `$env:XDG_CACHE_HOME/curl-gap/upstream`, falling back to `~/.cache/curl-gap/upstream`),
  `-ArchivePath` (use a local tarball instead of downloading; this is for self-tests),
  `-SelfTest`.
- URL: first
  `https://github.com/curl/curl/releases/download/curl-<8_21_0>/curl-<8.21.0>.tar.gz`,
  then `https://curl.se/download/curl-<8.21.0>.tar.gz`. curl's GitHub releases carry
  `.tar.gz` assets. This was checked for curl-8_22_0 on 2026-10-08, whose assets are
  `.tar.bz2`, `.tar.gz`, `.tar.xz` and `.zip`, each with `.asc`.
- Compute the SHA-256 with `Get-FileHash`. If `Gap/Baselines/curl-<version>.json` exists
  and its `sha256` differs, throw, name both hashes and leave the cache untouched. If the
  file does not exist, write it (trust on first use, ADR-0433 decision 1).
- Extract only `curl-<version>/docs`, `curl-<version>/tests/data`,
  `curl-<version>/lib/strerror.c` and `curl-<version>/COPYING` into
  `<CacheRoot>/<version>/`. Write `<CacheRoot>/<version>/.complete` holding the hash last,
  so an interrupted extraction is redone. When `.complete` holds the pinned hash, return
  at once without downloading.
- Output: the cache folder's full path, as the only line on standard output.
- Commit `Gap/Baselines/target.json` (`{ "version": "8.21.0", "decidedBy": "ADR-0433" }`)
  and `Gap/Baselines/curl-8.21.0.json`, made by running the script for real once.
- The script must run under Windows PowerShell 5.1 and PowerShell 7, and on Linux under
  `pwsh`. The weekly release watcher (BL-1736) runs it on `ubuntu-latest`.

`-SelfTest` builds a tiny `curl-9.9.9.tar.gz` in a temporary folder with `tar`, holding a
fake `docs/x.md`, `tests/data/test1`, `lib/strerror.c`, `lib/other.c` and `COPYING`. It
then runs the script against it with `-ArchivePath` and a temporary `-CacheRoot` and a
temporary baselines folder (add a `-BaselinesDirectory` parameter, default
`Gap/Baselines`). The fixture's parts may live under `Gap/Tools/Fixtures/release/`.

## Acceptance criteria

- [ ] `Gap/Tools/Get-UpstreamRelease.ps1 -SelfTest` prints `PASS` lines and no `FAIL`, and exits 0, under Windows PowerShell 5.1 and PowerShell 7. It checks five things: the first run writes the manifest with the archive's hash; only `docs/`, `tests/data/`, `lib/strerror.c` and `COPYING` are extracted (`lib/other.c` is not); a second run with the same archive returns without re-extracting; an archive whose hash differs from the manifest throws and changes nothing; the printed path is the version's cache folder.
- [ ] `Gap/Baselines/target.json` and `Gap/Baselines/curl-8.21.0.json` are committed. The latter holds the SHA-256 of the real `curl-8.21.0.tar.gz` and its URL.
- [ ] Running `powershell -NoProfile -File Gap/Tools/Get-UpstreamRelease.ps1` with no arguments prints a folder that contains `docs/cmdline-opts/write-out.md`, `docs/libcurl/libcurl-errors.md`, `lib/strerror.c` and more than 1900 `tests/data/test*` files.
- [ ] The header help documents every parameter, the cache locations and the hash rule. The script is ASCII only.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
