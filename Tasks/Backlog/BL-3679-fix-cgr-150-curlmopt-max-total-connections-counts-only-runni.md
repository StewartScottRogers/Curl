---
id: BL-3679
title: Fix CGR-150: CURLMOPT_MAX_TOTAL_CONNECTIONS counts only running transfers: idle cached connections stay open, so the limit is exceeded
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr150Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3679 — Fix CGR-150: CURLMOPT_MAX_TOTAL_CONNECTIONS counts only running transfers: idle cached connections stay open, so the limit is exceeded

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-150 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-150: CURLMOPT_MAX_TOTAL_CONNECTIONS counts only running transfers: idle cached connections stay open, so the limit is exceeded

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-11`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- command: `probe.exe C_09,C_66 2`.
  - `C_66_maxtotal_open_idle`: MAX_TOTAL_CONNECTIONS=1. Transfer A to :21700 completes, then transfer B goes to :21701. Server 2 records how many connections to :21700 are still open when B's request arrives.
  - `C_09_maxtotal1_twohosts`: the same limit, with 4 handles alternating between the two hosts.

Commands (run each with REF and with PORT):

```sh
probe.exe C_09,C_66 2
```

**Expected (real curl)**

- C_66: `open connections to A while B was requested=0`. C_09: each host gets 2 separate connections (`[GET /a0] | [GET /a1]`).

**Actual (Curl)**

- C_66: `open connections to A while B was requested=1`. C_09: each host's connection is kept and reused (`[GET /a0,GET /a1]`, `openAtEnd=1` on both servers), so 2 connections are open under a limit of 1.

**Notes**

- *Note:* C_09's `global-maxconc` line is not usable evidence, because REF also shows values above 1 there.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs:614-627` (`CanStart` counts running entries only).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr150Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-150 of the third-party gap report.
