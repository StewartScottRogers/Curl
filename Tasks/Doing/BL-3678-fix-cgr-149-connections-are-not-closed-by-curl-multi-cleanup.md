---
id: BL-3678
title: Fix CGR-149: Connections are not closed by curl_multi_cleanup or curl_easy_cleanup
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr149Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3678 — Fix CGR-149: Connections are not closed by curl_multi_cleanup or curl_easy_cleanup

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-149 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-149: Connections are not closed by curl_multi_cleanup or curl_easy_cleanup

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-10`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- command: `probe.exe C_60,C_61 2`.
  - `C_60_conn_after_multi_cleanup`: one keep-alive transfer in a multi, then remove_handle, multi_cleanup (wait 500 ms) and easy_cleanup (wait 500 ms). After each step the server counts the connections still open.
  - `C_61_cross_multi_reuse`: two multis, one after the other, each with a new easy handle.

Commands (run each with REF and with PORT):

```sh
probe.exe C_60,C_61 2
```

**Expected (real curl)**

- C_60: `open after multi_cleanup=0`, `open after easy_cleanup=0`. C_61: `openAtEnd=0`.

**Actual (Curl)**

- C_60: `open after multi_cleanup=1`, `open after easy_cleanup=1`. C_61: `openAtEnd=2`. Connections are left open at the end of nearly every multi case (`openAtEnd>=1`).

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs:289-308` (`DisposeAsync` does not close pooled connections).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr149Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-149 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
