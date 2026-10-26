---
id: BL-3541
title: Fix CGR-012: curl_multi_cleanup called from inside a transfer callback never returns in PORT, and the multi handle is destroyed under the caller (REF...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr012Tests.cs, Libcurl.UnitTests/Cgr012Tests.cs, Libcurl/Multi/Core/CurlMulti.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3541 — Fix CGR-012: curl_multi_cleanup called from inside a transfer callback never returns in PORT, and the multi handle is destroyed under the caller (REF...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-012 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-012: curl_multi_cleanup called from inside a transfer callback never returns in PORT, and the multi handle is destroyed under the caller (REF refuses with CURLM_RECURSIVE_API_CALL)

- **Severity:** Critical (hang: the callback thread deadlocks)
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed. Detail: Confirmed (Windows 2/2, Linux 1/1)
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-1`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- case `C_93_multi_cleanup_in_callback` (`probe\CasesRecursive.cs:64`).
  1. Multi `m` holds one easy handle for `http://127.0.0.1:21700/`.
  2. On its first call, the WRITEFUNCTION calls `curl_multi_cleanup(m)` and records what it returns.
  3. The loop drives the transfer with `curl_multi_perform` + `curl_multi_poll(100)` for up to 3 s, then calls `curl_multi_cleanup(m)`.

  Command: `EV\probe\out\probe.exe C_93 2`.

Commands (run each with REF and with PORT):

```sh
EV\probe\out\probe.exe C_93 2
```

**Expected (real curl)**

- prints `multi_cleanup=8 msgs 0` / `final multi_cleanup=0`. The call inside the callback returns CURLM_RECURSIVE_API_CALL and does nothing; the transfer completes.

**Actual (Curl)**

- `multi_cleanup=` is never recorded, because the call inside the callback never returns. The application's next `curl_multi_perform` throws `ObjectDisposedException: Cannot access a disposed object.`, and no CURLMSG_DONE is delivered.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs:289-308`. `DisposeAsync` sets `disposed`, then runs `await Task.WhenAll(performs)`, and that list includes the perform task the callback is running on. `Libcurl\Multi\Core\CurlMulti.cs` has no check for a call made from inside a callback.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr012Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-012 of the third-party gap report.
