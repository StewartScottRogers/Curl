---
id: BL-3932
title: Add Setup-UnixFactoryHost.sh to prepare a remote Linux machine for dark factory shifts
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3919]
touches: [Setup-UnixFactoryHost.sh, Documentation/Wiki/Running-On-Linux-And-macOS.md]
requirement: none
created: 2026-10-27
completed:
---
# BL-3932 — Add Setup-UnixFactoryHost.sh to prepare a remote Linux machine for dark factory shifts

## Goal

Running `Setup-UnixFactoryHost.sh` once on a fresh remote Linux machine (Ubuntu or Debian first,
macOS where cheap) leaves it ready to run `RunDarkFactory.sh -NewTab -Lanes Auto -Continuous`, and
a wiki page tells Stewart, step by step, how to set it up and watch it from his desktop.

## Context

Part of BL-3934. Stewart wants to try running the dark factory on a remote Unix machine instead of
his desktop (2026-10-27). `hrdrClaudeNative.sh` already installs git, Node.js, Claude Code and
herdr and clones the repo; reuse it (call it or share its approach) rather than duplicating it.
On top of that the host needs: the .NET SDK pinned by `global.json` (10.0.x), PowerShell 7
(`pwsh`), the GitHub CLI `gh`, a `powershell` shim on PATH pointing at `pwsh` (so prompt text and
skills that say `powershell -NoProfile ...` still work for lane runs), the audio-off file in the
factory's data folder (`Get-FactoryDataDir` from BL-3919; nobody listens on a remote host), and
git's identity. Logins cannot be scripted: `claude` sign-in, `gh auth login`, and git push
credentials; the script checks each and prints what to run. Watching: `herdr --remote <ssh-target>`
from the desktop.

## Acceptance criteria

- [ ] `Setup-UnixFactoryHost.sh` exists at the repository root, executable with LF line ends, idempotent (a second run changes nothing and says so), and installs or verifies each item in Context, using the package manager or the vendors' official install scripts.
- [ ] It ends with a checklist that marks each item present or missing, including the three logins, and exits non-zero while any is missing.
- [ ] It was run in a fresh Ubuntu container or WSL distribution and reached an all-present checklist apart from the logins; the transcript summary is in the Log.
- [ ] `Documentation/Wiki/Running-On-Linux-And-macOS.md` explains, in numbered steps, preparing the host, logging in, starting a shift and watching it with `herdr --remote`, and is linked from the wiki's index.
- [ ] `dotnet build` and the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
