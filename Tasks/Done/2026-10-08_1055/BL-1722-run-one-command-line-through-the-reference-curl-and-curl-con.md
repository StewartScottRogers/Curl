---
id: BL-1722
title: Run one command line through the reference curl and Curl.Console side by side with Gap/Tools/Invoke-GapProbe.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1721]
touches: [Gap/Tools/Invoke-GapProbe.ps1, Gap/Tools/Fixtures/probe]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1722 — Run one command line through the reference curl and Curl.Console side by side with Gap/Tools/Invoke-GapProbe.ps1

## Goal

The area measurement tools (BL-1723 to BL-1727) all call one script to find the matched
reference curl and to run a command line through it and through `Curl.Console`, each in a
controlled environment. They get back each binary's exit code, stdout bytes and stderr text.

## Context

ADR-0433 decision 1 fixes the matched reference build. On Windows it is the Schannel mingw
curl from Git for Windows (ADR-0018). On Linux and macOS it is the OpenSSL curl on `PATH`.
The reference is used only when its `--version` first line names the targeted version from
`Gap/Baselines/target.json` (BL-1721). Otherwise the probe reports `reference: null` and the
caller falls back to what the release's documents say.

`Record-CurlExchange.ps1` (repository root) already finds the Windows reference in its
`Get-ReferenceCurlPath` function: `git.exe`'s `<Git>\mingw64\bin\curl.exe`, then
`$env:ProgramFiles\Git\mingw64\bin\curl.exe`. Use the same lookup. You may copy the function
into this script. Do not dot-source `Record-CurlExchange.ps1`, which runs a server when
loaded.

Write it as a script that can be dot-sourced to get functions, and also run directly:

- `Get-GapReferenceCurl [-TargetVersion]` returns `{ Path, VersionLine, Matches }`, or
  `$null` when no reference is found.
- `Get-GapCandidateCurl [-Path]` returns the `Curl.Console` binary. That is the path given,
  or else the newest `curl.exe` (`curl` off Windows) under `Curl.Console/bin/Release`. It
  throws with the `dotnet build Curl.Console -c Release` command when there is none.
- `Invoke-GapProbe -Arguments <string[]> [-Environment <hashtable>] [-WorkingDirectory]
  [-StandardInput <byte[]>] [-TimeoutSeconds 20]` runs the command line through the
  reference (when it matches) and the candidate, one after the other. It returns
  `{ Reference = { ExitCode, Stdout (byte[]), Stderr (string) } or $null, Candidate = {
  ... } }`.
- The environment is controlled. Each run starts from an environment holding only
  `PATH`, `SystemRoot`, `TEMP`, `TMP`, `USERPROFILE`, `HOME` and `APPDATA`, with
  `HOME`, `USERPROFILE`, `APPDATA`, `CURL_HOME` and `XDG_CONFIG_HOME` pointed at a fresh
  empty temporary folder. No real `.curlrc` or `_curlrc` can then leak in. `-Environment`
  adds to or overrides that set, and a `$null` value removes a variable.
- Each run is killed after `-TimeoutSeconds` and reported as `ExitCode = $null` with
  `TimedOut = $true`.
- Arguments are quoted for the Windows command line the way `Record-CurlExchange.ps1`'s
  `CurlArgs` handling quotes them, so an argument with spaces or quotes arrives intact.

`-SelfTest` exercises the environment control and the quoting without needing either curl.
It uses the PowerShell host itself as a stand-in executable: add `-CandidatePath` and
`-ReferencePath` overrides so the self-test can point both at a tiny script runner that
echoes its arguments and environment, kept under `Gap/Tools/Fixtures/probe/`.

## Acceptance criteria

- [x] `Gap/Tools/Invoke-GapProbe.ps1 -SelfTest` prints `PASS` lines and no `FAIL`, and exits 0, under Windows PowerShell 5.1 and PowerShell 7. It checks five things: an argument with a space and a double quote arrives as one argument; `HOME`, `USERPROFILE`, `APPDATA`, `CURL_HOME` and `XDG_CONFIG_HOME` point at an empty folder; an `-Environment` entry is passed and a `$null` entry removes the variable; a run past `-TimeoutSeconds` reports `TimedOut`; a reference whose version line names another version is reported as not matching.
- [x] On a Windows machine with Git for Windows, `. Gap/Tools/Invoke-GapProbe.ps1; Get-GapReferenceCurl` returns the `mingw64\bin\curl.exe` path and its `curl 8.21.0` version line.
- [x] Each function has comment-based help. The script is ASCII only.

## Notes

- A `.ps1` path given as `-CandidatePath` or `-ReferencePath` runs through the current
  PowerShell host with `-File`; when that host is pwsh installed as a .NET tool (its
  process is `dotnet.exe`), `$PSHOME\pwsh.dll` is passed first. This is how the self-test
  stands `Gap/Tools/Fixtures/probe/Write-ProbeEcho.ps1` in for both binaries.
- The PowerShell stand-in itself creates an `AppData` (pwsh) or `Microsoft` (5.1) folder
  in the empty profile folder as it starts, so the empty-folder check allows those two
  names and nothing else. A real curl creates nothing there.
- `Get-GapReferenceCurl` matches when the `--version` first line starts `curl <target> `
  (ordinal). Off Windows the reference is the first `curl` application on PATH.
- On timeout the process is killed with `Process.Kill()` (no tree kill: Windows
  PowerShell 5.1 has no `Kill($true)`); curl starts no children, so this suffices.
- Verified on this machine: self-test 5 PASS under pwsh 7 and Windows PowerShell 5.1;
  `Get-GapReferenceCurl` returns `C:\Program Files\Git\mingw64\bin\curl.exe`,
  `curl 8.21.0 (x86_64-w64-mingw32) ... Schannel ...`, Matches True.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Gap/Tools/Invoke-GapProbe.ps1 runs a command line through the matched reference curl and Curl.Console in a controlled environment; -SelfTest passes on PS 5.1 and 7
