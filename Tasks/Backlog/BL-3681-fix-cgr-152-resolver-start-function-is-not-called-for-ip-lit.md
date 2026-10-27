---
id: BL-3681
title: Fix CGR-152: RESOLVER_START_FUNCTION is not called for IP-literal hosts, so its abort has no effect
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3680]
touches: [Curl.Engine.UnitLibrary/ResolverStartDnsResolver.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitTests/Cgr152Tests.cs, Curl.Engine.UnitTests/EasyHandleIsolationTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3681 — Fix CGR-152: RESOLVER_START_FUNCTION is not called for IP-literal hosts, so its abort has no effect

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-152 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-152: RESOLVER_START_FUNCTION is not called for IP-literal hosts, so its abort has no effect

- **Severity:** Medium
- **Area:** libcurl-multi-callbacks
- **Platform(s):** Both (Windows and Linux). Detail: Windows, Linux
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-multi-callbacks LMC-13`

**Reproduction**

- *Setup (libcurl-multi-callbacks):* EV = `SCR\evidence\libcurl-multi-callbacks`. A C# probe (`EV\probe\`) runs one driver per case against REF `libcurl-4.dll` (P/Invoke) and PORT `Libcurl.dll` (its C# API), with a built-in recording server: 127.0.0.1:21700 answers `200 hello`, 21701 answers `200 other`, 21799 is closed. Run `EV\probe\out\probe.exe <cases> 2` or `powershell -File EV\run.ps1 -Filter <cases> -Out <name> -Reps 2` (`LIBONLY=REF` or `LIBONLY=PORT` runs one side). Linux: `wsl -d Ubuntu -- bash "<EV as /mnt/c path>/linux.sh" <cases> 1 <name>`.
- case `C_71_resolver_ip_literal`. The URLs `http://127.0.0.1:21700/` and `http://[::1]:21799/` are each run with a RESOLVER_START_FUNCTION that returns 0, then 1. Command: `probe.exe C_71 2`.

Commands (run each with REF and with PORT):

```sh
probe.exe C_71 2
```

**Expected (real curl)**

- the callback is called once (`calls=1`) for every case. With ret=1, both URLs give `rc=42`.

**Actual (Curl)**

- `127.0.0.1 ret=1: rc=0 calls=0`, and the request is sent. `[::1] ret=1: rc=7 calls=0`, because the earlier lookup is cached.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ResolverStartDnsResolver.cs:19-29`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr152Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 1, first claim): **Root cause found, same as BL-3680 (CGR-151).** `ResolverStartDnsResolver` is fine: it is called for IP literals too, and an engine-level test (`Cgr152Tests`, uncommitted, stashed with this run) of a fresh handle with ret=1 on `http://127.0.0.1:21700/` and `http://[::1]:21799/` already passes (`AbortedByCallback`, `Could not resolve host: <literal>`, one call). The gap is that `ManagedEngineSharedState.WithOwnCaches()` (line 118) hands every library easy handle the engine's `DnsCache`, so the second handle finds the first one's literal cached and never calls the callback. That file is in BL-3680's `touches` (lane 6, Doing), so this task now depends on BL-3680; the touches gained `ManagedEngineSharedState.cs` and `EasyHandleIsolationTests.cs` (its `WithOwnCaches_EngineState_GivesFreshCachesOverTheEnginesNumbering` asserts `AreSame(engine.Shared.DnsCache, own.DnsCache)`, which the fix must flip).
- Measured with a C# file-based probe (outside the repo) loading real libcurl 8.22.0 Schannel (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`, `curl_easy_*` through `NativeLibrary`) and PORT through `Libcurl`'s `CurlGlobal.Init().EasyInit()`, against a loopback server on 127.0.0.1:21700 answering `200 hello`, 21799 closed. URL × callback ret 0 then 1:
  - fresh handle per run: REF `127.0.0.1 ret=0 rc=0 calls=1 req=1`, `ret=1 rc=42 calls=1 req=0`, `[::1] ret=0 rc=7 calls=1`, `ret=1 rc=42 calls=1`. PORT: `ret=0` the same, but `127.0.0.1 ret=1 rc=0 calls=0 req=1` and `[::1] ret=1 rc=7 calls=0` (the CGR-152 repro).
  - one handle for both runs: REF and PORT agree (`ret=1`: `rc=0 calls=0` reusing the connection, `[::1] rc=7 calls=0` from the handle's cache), so this variant already matches.
- Measured for ADR-0789's test1512 claim: handle A with `CURLOPT_RESOLVE fake.example:21700:127.0.0.1`, then a fresh handle B to `http://fake.example:21700/` with no RESOLVE: REF B gives `rc=6 calls=1`, with or without `CURLOPT_DNS_USE_GLOBAL_CACHE` 1 on both; PORT B gives `rc=0 calls=0`. So libcurl 8.22.0's easy handles do not see each other's resolved names (`dnscache_get` in `lib/vdns/dnscache.c` returns the share's or the handle's multi's cache only), contrary to ADR-0789. Giving each handle its own `DnsCache` may therefore change upstream test1512 on the ratchet (`PassingUpstreamCases.txt` line 1483); whoever lands the fix (BL-3680) should run that case and amend ADR-0789.
- After BL-3680 lands: rerun the fresh-handle probe above, keep `Cgr152Tests` (it must also gain a two-handle case: A with a RESOLVE entry for one host and ret=1 on another, then B to that host with ret=1 expecting `AbortedByCallback` and one call), and finish.

## Log

- 2026-10-26: Created from CGR-152 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3680 (Doing, lane 6): its ManagedEngineSharedState.cs DNS cache fix is this issue's root cause
