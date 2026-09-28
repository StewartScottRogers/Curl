---
id: BL-029
title: Make the download page and installers find pre-releases
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-028]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-029 — Make the download page and installers find pre-releases

## Goal

The download links and both installers work while the only releases are pre-releases.

## Context

GitHub's `releases/latest/download/...` URLs skip pre-releases, so after
`v0.1.0-preview.1` every link in `DOWNLOAD.md` and the default install path of
`install.sh` and `install.ps1` returned 404.

## Acceptance criteria

- [x] `install.sh` and `install.ps1` with no version install the newest release, pre-releases included.
- [x] Every package link in `DOWNLOAD.md` returns HTTP 200.
- [x] `install.ps1` (Windows) and `install.sh` (Linux, via WSL) each install `v0.1.0-preview.1` into a scratch directory and the binary starts.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Installers resolve the newest release including pre-releases; DOWNLOAD.md links by tag and all seven return 200.
