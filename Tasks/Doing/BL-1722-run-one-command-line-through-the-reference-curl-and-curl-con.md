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
completed:
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

- [ ] `Gap/Tools/Invoke-GapProbe.ps1 -SelfTest` prints `PASS` lines and no `FAIL`, and exits 0, under Windows PowerShell 5.1 and PowerShell 7. It checks five things: an argument with a space and a double quote arrives as one argument; `HOME`, `USERPROFILE`, `APPDATA`, `CURL_HOME` and `XDG_CONFIG_HOME` point at an empty folder; an `-Environment` entry is passed and a `$null` entry removes the variable; a run past `-TimeoutSeconds` reports `TimedOut`; a reference whose version line names another version is reported as not matching.
- [ ] On a Windows machine with Git for Windows, `. Gap/Tools/Invoke-GapProbe.ps1; Get-GapReferenceCurl` returns the `mingw64\bin\curl.exe` path and its `curl 8.21.0` version line.
- [ ] Each function has comment-based help. The script is ASCII only.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
