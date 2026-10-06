---
id: BL-028
title: Publish native curl binaries for Windows, Linux and macOS with a download page
priority: High
assignee: Claude
pipeline: direct
depends-on: []
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-028 — Publish native curl binaries for Windows, Linux and macOS with a download page

## Goal

Every push builds and tests Curl on Windows, Linux and macOS, a `v*` tag publishes a
native-AOT `curl` binary for each supported platform to a GitHub release, and a
download page linked from the README says how to download and install each one.

## Context

Requested by Stewart on 2026-09-26. `Curl.Console` already publishes native AOT
(`PublishAot` defaults to true) and every library is BCL-only, so no code change is
expected; native AOT cannot cross-compile between operating systems, so each OS
publishes on its own runner. Tags and releases need Stewart's confirmation
(`CLAUDE.md`), so this task delivers the workflow and page but does not tag.

## Acceptance criteria

- [x] `.github/workflows/ci.yml` builds and runs the fast tests on `ubuntu`, `windows` and `macos` runners.
- [x] `.github/workflows/release.yml` publishes `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64` binaries, packaged with a `SHA256SUMS` file, on a `v*` tag; run by hand it uploads the packages as workflow artifacts only.
- [x] `install.sh` and `install.ps1` at the repository root download and install the right package for the machine they run on.
- [x] `DOWNLOAD.md` lists every supported platform with its download link and install steps, and `README.md` links to it.
- [x] A dry run of `release.yml` succeeds for all six platforms (PR #4, run 36242090134).

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. CI builds and tests on Windows, Linux and macOS; release.yml publishes six native binaries on a v* tag; DOWNLOAD.md and installers linked from the README.
