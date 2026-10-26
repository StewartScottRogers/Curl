---
id: BL-3677
title: Fix CGR-148: The socket callback is invoked from curl_multi_perform, and a -1 return fails the transfer with 42 (REF leaves it running or completes it)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr148Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3677 — Fix CGR-148: The socket callback is invoked from curl_multi_perform, and a -1 return fails the transfer with 42 (REF leaves it running or completes it)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-148 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-148: The socket callback is invoked from curl_multi_perform, and a -1 return fails the transfer with 42 (REF leaves it running or completes it)

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-9`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- cases `C_23_sock_cb_abort` and `C_78_sock_cb_abort_long`. SOCKETFUNCTION returns -1 on its first call, or TIMERFUNCTION returns -1 on its second. The multi is driven for 3 s, either with `socket_action(CURL_SOCKET_TIMEOUT)` every 100 ms or with perform + poll. Command: `probe.exe C_23,C_78 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_23,C_78 2
```

**Expected (real curl)**

- socket_action: `codes 11/1 0/1 0/1 ...` (CURLM_ABORTED_BY_CALLBACK once, then 0 with running=1), no DONE message, and the callback is not called again.
- curl_multi_perform: `socket cb calls=0`, and the transfer completes with result 0.

**Actual (Curl)**

- socket_action: `codes 11/1 11/0 0/0 ... msgs 42`, and the callback gets a second call (`S=4`, CURL_POLL_REMOVE).
- curl_multi_perform: `socket cb calls=2 codes 11/1 0/0 msgs 42`.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs:684` (`UpdateSockets` runs on every perform), `:726-737` and `:749-767`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr148Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-148 of the third-party gap report.
