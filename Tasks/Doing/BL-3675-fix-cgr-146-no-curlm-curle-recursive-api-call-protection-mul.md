---
id: BL-3675
title: Fix CGR-146: No CURLM/CURLE_RECURSIVE_API_CALL protection: multi and easy API calls made inside callbacks succeed in PORT and run nested transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitTests/Cgr146Tests.cs, Libcurl.UnitTests/Cgr146Tests.cs, Libcurl/Multi/Core/CurlMulti.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3675 — Fix CGR-146: No CURLM/CURLE_RECURSIVE_API_CALL protection: multi and easy API calls made inside callbacks succeed in PORT and run nested transfers

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-146 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-146: No CURLM/CURLE_RECURSIVE_API_CALL protection: multi and easy API calls made inside callbacks succeed in PORT and run nested transfers

- **Severity:** Medium (affects only applications that make calls libcurl forbids inside callbacks, but the wire traffic and messages change)
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-7`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- two cases. Command: `probe.exe C_90,C_91 2`.
  - `C_90_recursive_in_multi`: `e1` (`/one`) is in multi `m`. On its first call, e1's WRITEFUNCTION runs, in order: `curl_multi_perform`, `curl_multi_add_handle(e2 /two)`, `curl_multi_remove_handle(e1)`, `curl_easy_perform(e3 /three)`, `curl_multi_wait`, `curl_multi_poll`, `curl_multi_socket_action(TIMEOUT)`, `curl_multi_timeout`, `curl_multi_info_read`, `curl_multi_wakeup`.
  - `C_91_recursive_in_easy`: inside its own WRITEFUNCTION, `e1` calls `curl_easy_perform(e1)` (itself), then `curl_easy_perform(e2)`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_90,C_91 2
```

**Expected (real curl)**

- C_90: every multi call returns 8; info_read returns NULL; wakeup returns 0; the unrelated easy_perform returns 0. The only message is `msgs e1:0`, and the server sees `[GET /one] | [GET /three]`.
- C_91: `curl_easy_perform(self)` is refused: 93 on Windows 8.22, 2 on Linux 8.18.

**Actual (Curl)**

- C_90: every call returns 0 and takes effect. The only message is `msgs e2:0`, and the server sees `[GET /one] | [GET /three] | [GET /two]`.
- C_91: `curl_easy_perform(self)` returns 0 after a complete nested request (the server sees `[GET /one]` twice).

**Suspected location in source**

- none of these checks for a call made inside a callback: `Libcurl\Multi\Core\CurlMulti.cs:44-110`, `Curl.Engine.UnitLibrary\ManagedMultiTransfer.cs:674-686`, `Curl.Engine.UnitLibrary\ManagedEasyTransfer.Perform.cs:26`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr146Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-146 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
