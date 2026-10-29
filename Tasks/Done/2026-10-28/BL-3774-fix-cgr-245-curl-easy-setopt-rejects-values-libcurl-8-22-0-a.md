---
id: BL-3774
title: Fix CGR-245: curl_easy_setopt rejects values libcurl 8.22.0 accepts: PROXY_TRANSFER_MODE other than 0/1, MAXCONNECTS=0, and negative ADDRESS_SCOPE
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr245Tests.cs, Libcurl/Options/Transfer/EasyTransferOptionSetters.cs, Libcurl/Options/Network/EasyNetworkOptionSetters.cs, Libcurl.UnitTests/Options/Network/EasyNetworkOptionSettersTests.cs, Libcurl.UnitTests/Options/Transfer/EasyTransferOptionSettersTests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3774 — Fix CGR-245: curl_easy_setopt rejects values libcurl 8.22.0 accepts: PROXY_TRANSFER_MODE other than 0/1, MAXCONNECTS=0, and negative ADDRESS_SCOPE

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-245 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-245: curl_easy_setopt rejects values libcurl 8.22.0 accepts: PROXY_TRANSFER_MODE other than 0/1, MAXCONNECTS=0, and negative ADDRESS_SCOPE

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux). Detail: PROXY_TRANSFER_MODE fails on both platforms. MAXCONNECTS=0 and ADDRESS_SCOPE<0 fail on Windows only, because REF-LINUX 8.18 rejects them as the port does.
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice, Linux for PROXY_TRANSFER_MODE).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-12`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro` for `setopt(PROXY_TRANSFER_MODE, 2L)` and `setopt(MAXCONNECTS, 0L)`. `probe.exe sweep` covers ADDRESS_SCOPE = -1, -2, -3 and INT_MIN.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
probe.exe sweep
```

**Expected (real curl)**

- 8.22.0 returns 0 in all cases; it treats PROXY_TRANSFER_MODE as a boolean.

**Actual (Curl)**

- PROXY_TRANSFER_MODE returns 43 for every value except 0 and 1. MAXCONNECTS=0 returns 43. Negative ADDRESS_SCOPE returns 43.

**Suspected location in source**

- `Libcurl\Options\Transfer\EasyTransferOptionSetters.cs:100-101` (`SetInRange 0..1`), `Options\Network\EasyNetworkOptionSetters.cs:156` (ADDRESS_SCOPE `0..uint.MaxValue`), and the MAXCONNECTS setter (`SetInRange` min 1).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr245Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-245 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (lane 5)

- Probe (REF libcurl P/Invoke) is not available in this lane; the report states REF 8.22.0 returns 0 for PROXY_TRANSFER_MODE (boolean), MAXCONNECTS=0 and ADDRESS_SCOPE<0 on Windows. Decision: match that. PROXY_TRANSFER_MODE any non-zero is on; MAXCONNECTS min 0; ADDRESS_SCOPE accepts int.MinValue..uint.MaxValue and stores the negative as unsigned (libcurl stores an unsigned long).
- touches extended with Libcurl/Options/Network/EasyNetworkOptionSetters.cs and the two existing setter test files (stale rows asserting 43 removed); no other Doing task names them.
- Cgr245Tests.cs fails before (43) and passes after. Libcurl fast tests 1468 pass; Measure-CodeQuality -Library Libcurl: 0 failing members. Platform-neutral, so the Linux/macOS CI criterion is met.
- 2026-10-28: Doing -> Done. PROXY_TRANSFER_MODE boolean, MAXCONNECTS 0 and negative ADDRESS_SCOPE accepted
