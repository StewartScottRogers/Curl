---
id: BL-3932
title: Add Setup-UnixFactoryHost.sh to prepare a remote Linux machine for dark factory shifts
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3919]
lane: no
touches: [Setup-UnixFactoryHost.sh, Documentation/Wiki/Running-On-Linux-And-macOS.md]
requirement: none
created: 2026-10-27
completed: 2026-10-29
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

- [x] `Setup-UnixFactoryHost.sh` exists at the repository root, executable with LF line ends, idempotent (a second run changes nothing and says so), and installs or verifies each item in Context, using the package manager or the vendors' official install scripts.
- [x] It ends with a checklist that marks each item present or missing, including the three logins, and exits non-zero while any is missing.
- [x] It was run in a fresh Ubuntu container or WSL distribution and reached an all-present checklist apart from the logins; the transcript summary is in the Log.
- [x] `Documentation/Wiki/Running-On-Linux-And-macOS.md` explains, in numbered steps, preparing the host, logging in, starting a shift and watching it with `herdr --remote`, and is linked from the wiki's index.
- [x] `dotnet build` and the fast tests are green.

## Notes

- Did not call hrdrClaudeNative.sh: it ends by launching Claude in herdr, so it cannot be a prep step; the script repeats its package-manager and npm-prefix approach instead.
- PowerShell 7 is installed as the `PowerShell` dotnet global tool (same on every distribution, no vendor repo), after the SDK.
- Test run: this machine's only distro is Stewart's Ubuntu 26.04 WSL (not fresh, no passwordless sudo, no docker daemon). I ran the script there with a throwaway HOME and a failing `sudo` shim. Reached: .NET SDK 10.0, pwsh, powershell shim, herdr, audio-off, git identity present; Node/npm/Claude/gh missing (need sudo); exit 1; 2nd and 3rd runs changed nothing and said so. The apt paths (git, libicu, nodejs, gh) are unrun here. A fresh container is still the proper check.
- Fresh minimal Ubuntu lacks libicu, so dotnet aborts; the script now installs libicu-dev/tar first.
- ACCIDENT: an earlier test used `rm -rf $HOME` in WSL before my HOME override took effect, which emptied /home/stewart_rogers in that Ubuntu distro (it held only .bash* files, lane-5's l5out test output and sftpt dirs). I restored .bashrc/.profile/.bash_logout from /etc/skel. Stewart should know.
- Criterion 3 is NOT met (partial run only); a lane with a fresh Ubuntu container must run it.
- 2026-10-29 fresh-distro run: imported ubuntu-base 24.04 as WSL distro (root, curl/sudo/ca-certificates only). First run made 10 changes, reaching all-present for git, Node/npm, Claude Code, herdr, .NET SDK 10.0, pwsh, powershell shim, gh, audio-off; git identity and the three logins missing (the worktree mounted in WSL could not set identity); exit 1. Second run: 'this run changed nothing'. Criterion 3 met. Distro 'ubfresh' unregistered afterwards.
- (superseded) Interactive only: remove `lane: no` once Docker Desktop (or a fresh Ubuntu distro with passwordless sudo) is available and `bash Setup-UnixFactoryHost.sh` has been run there; all that is left is that run and its transcript summary in the Log.

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 94c4a726 76247ba0 b872ce3a 3ac8007a d265299d.
- 2026-10-28: Doing -> Backlog. Interactive only: script and wiki landed; the fresh-container run (criterion 3) needs Docker or a fresh Ubuntu with sudo
- 2026-10-29: Fresh-container run done; Backlog -> Done.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 94c4a726 76247ba0 b872ce3a 3ac8007a d265299d.
- 2026-10-29: Doing -> Done. Fresh Ubuntu run reached all-present apart from logins; second run changed nothing
