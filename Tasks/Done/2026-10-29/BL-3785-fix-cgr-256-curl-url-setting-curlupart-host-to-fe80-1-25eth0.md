---
id: BL-3785
title: Fix CGR-256: curl_url: setting CURLUPART_HOST to [fe80::1%25eth0] makes HOST read back without the zone id
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartText.cs, Curl.Engine.UnitTests/Cgr256Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3785 — Fix CGR-256: curl_url: setting CURLUPART_HOST to [fe80::1%25eth0] makes HOST read back without the zone id

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-256 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-256: curl_url: setting CURLUPART_HOST to `[fe80::1%25eth0]` makes HOST read back without the zone id

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-23`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro` (curl_url section). It sets URL to `http://example.com/`, sets HOST to `[fe80::1%25eth0]`, then reads HOST, ZONEID and URL.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
```

**Expected (real curl)**

- HOST=`[fe80::1%25eth0]`, ZONEID=`eth0`, URL=`http://[fe80::1%25eth0%25eth0]/` (REF writes the zone twice).

**Actual (Curl)**

- HOST=`[fe80::1]`, ZONEID=`eth0`, URL=`http://[fe80::1%25eth0]/`.

**Suspected location in source**

- `Curl.Engine.UnitLibrary` (ICurlUrlHandle Set Host).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr256Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Decided (ADR-0983): `IEasyTransfer.PerformingTransfer` (default: the handle itself) names the handle whose perform does the transfer; `StoreBoundEasyTransfer` forwards it, and `ManagedMultiTransfer.Start` gives the socket wait and multi callbacks to the `PerformingTransfer` when it is a `ManagedEasyTransfer`. The seam names the handle, not the wait, because `TransferSocketWait` is internal to the engine. A fake that wraps no engine transfer keeps the synthetic `FirstTransferSocket` number (it has no socket to tell).
- Decided (ADR-0983): while a running transfer on a socket wait waits on nothing after the settle, the timer is 0 (libcurl's `Curl_expire(data, 0)`), also for one whose perform ended and waits to be reported; the 10 ms interval stays only for transfers with no socket wait.
- Added `Curl.Api.UnitTests/Stores/StoreBoundEasyTransferTests.cs` to touches: Curl.Api's 100% coverage gate needs the forwarder tested; no task in Doing on origin/work/dark-factory names it.
- `RunningTimerMilliseconds` went past the complexity gate (cached method-group delegates count); split into `CheckIntervalMilliseconds`. Measure-CodeQuality for Curl.Engine.UnitLibrary, Curl.Api, Curl.Api.UnitLibrary: 0 failing members. Fast tests green on Windows; the tests use no path, OS error or certificate and wait on the transfer's own events, so they are platform-neutral.
- Repro (curl_url section of probe `repro`) measured per the report: REF gives HOST `[fe80::1%25eth0]`, ZONEID `eth0`, URL `http://[fe80::1%25eth0%25eth0]/`. Fixed in `UrlPartSetter.CheckedHost`: a bracketed host with a zone keeps its typed text as the host, zone id kept apart. Curl now matches; unit test `Set_HostWithZoneId_ReadsBackWithTheZoneAndTheUrlWritesItTwice` (in Cgr256Tests.cs) failed before, passes after. `Set_HostAnIPv6AddressWithAZone_KeepsTheZoneApart` updated to the new read-back. Zone-less hosts unchanged. Platform-neutral; Engine fast tests green, quality run: 0 failing members.

## Log

- 2026-10-26: Created from CGR-256 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. CGR-256 fixed: host set with a zone reads back with it
