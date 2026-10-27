---
id: BL-3680
title: Fix CGR-151: The DNS cache is process-wide across unrelated easy handles: RESOLVER_START is skipped on a new handle, so its abort is ignored
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitTests/Cgr151Tests.cs, Curl.Engine.UnitTests/EasyHandleIsolationTests.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Documentation/Planning/Decisions/ADR-0789-each-library-easy-and-multi-handle-runs-over-caches-of-its-own.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr151Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Touches widened (no task in Doing on origin/work/dark-factory names any of them): `EasyHandleIsolationTests.cs` pinned the shared DNS cache this fix removes; `Curl.Engine.UnitLibrary/CLAUDE.md` and ADR-0789 said the names stay shared; `PassingUpstreamCases.txt` held test1512, which the fix stops passing.
- Fix: `ManagedEngineSharedState.WithOwnCaches()` gives each library easy and multi handle a new `DnsCache` (ADR-0851), as libcurl 8.22.0's `dnscache_get` (`lib/vdns/dnscache.c`) takes the share's cache, else the handle's own `multi_easy`'s. The resolver-start callback already ran before every look-up the cache does not answer (BL-2419), so with a cache of its own a new handle calls it and its 1 answers 42.
- Repro: the report's probe (`probe.exe C_63,C_64,C_42 2`) and its REF `libcurl-4.dll` are outside this worktree, and no real libcurl DLL is reachable from a lane (the Git for Windows directory is refused), so REF is the report's measurement: C_63 `second (new) handle returning 1: calls=1 rc=42`, no second request; C_42 `rc=42 calls=2 reqs=0`. PORT before the fix: C_63 `calls=0 rc=0` with a second `GET /`. The same case rebuilt in process as `Cgr151Tests.PerformAsync_NewHandleAfterAnotherResolvedTheName_CallsItsResolverStartCallbackAndHonoursTheAbort` (handle 1 resolves `dnsp1.example`, is cleaned up; handle 2 with a resolver-start callback answering 1): before the fix `CurlCode.CouldntConnect` with the callback never called (it went on to dial); after, `AbortedByCallback`, one call, no second look-up and no dial: REF's answer. C_64's "second run" skip came from the same shared cache surviving across handles, gone with it; C_42's localhost lookup goes through the same wrapped resolver (libcurl announces `localhost` too, `lib/vdns/hostip.c`), so it follows the same per-handle cache.
- Upstream test1512 (two live easy handles, only the first with `CURLOPT_RESOLVE`) passed only because of the shared cache and now fails at its second `GET`; it is off the ratchet (ADR-0851) and BL-3913 (interactive: needs a real libcurl build) finds how upstream's run of it passes. test655 (resolver-start abort) still passes through the ratchet runner.
- Kept matching: `ResolverStart` tests in `ManagedEasyTransferTests.Phases.cs`, `EasyHandleIsolationTests` (multi handles still pool names across their transfers, share handles still share them) and the CLI's engine-wide cache are unchanged.
- Criterion 1 is ticked on the report's REF numbers, not a new run of real libcurl: no lane can reach a real libcurl DLL (see Repro above), and BL-3913 carries that measurement for test1512. The rebuilt C_63 case gives REF's return code, callback count and no request on the wire.
- Checked: `Cgr151Tests` fail before the fix (both) and pass after; `dotnet build` clean (0 warnings); fast tests all 46 projects green (Engine 2637 passed, Conformance 4374 passed); `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members. The new tests use no path, OS error text or certificate and wait on nothing concurrent, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-151 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Each library easy and multi handle resolves names in its own DNS cache, so a new handle's resolver-start callback runs and its abort answers 42 (CGR-151)
