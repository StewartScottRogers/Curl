---
id: BL-3680
title: Fix CGR-151: The DNS cache is process-wide across unrelated easy handles: RESOLVER_START is skipped on a new handle, so its abort is ignored
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitTests/Cgr151Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3680 — Fix CGR-151: The DNS cache is process-wide across unrelated easy handles: RESOLVER_START is skipped on a new handle, so its abort is ignored

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-151 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-151: The DNS cache is process-wide across unrelated easy handles: RESOLVER_START is skipped on a new handle, so its abort is ignored

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-12`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- case `C_63_dns_cache_across_handles`. Easy handle e1 performs `http://dnsp1.localhost:21700/` and is cleaned up. A new easy handle e2, with no share handle, sets a RESOLVER_START_FUNCTION that returns 1, and performs the same URL. Cases `C_42` and `C_64` show the same effect within one process. Command: `probe.exe C_63,C_64,C_42 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_63,C_64,C_42 2
```

**Expected (real curl)**

- `second (new) handle returning 1: calls=1 rc=42`, and no second request.

**Actual (Curl)**

- C_63: `calls=0 rc=0`, and a second `GET /` is sent.
- C_42: `localhost ret=1: rc=0 calls=0 reqs=2` (REF: `rc=42 calls=2 reqs=0`).
- C_64, second run: even the first lookup skips the callback (`ret=0: calls=0`).

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedEngineSharedState.cs:118` and `:144` (each handle's state is built with the shared DnsCache).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr151Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-151 of the third-party gap report.
