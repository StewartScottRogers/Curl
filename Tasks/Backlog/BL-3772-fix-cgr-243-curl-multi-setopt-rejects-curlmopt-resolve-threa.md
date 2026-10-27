---
id: BL-3772
title: Fix CGR-243: curl_multi_setopt rejects CURLMOPT_RESOLVE_THREADS_MAX (20) and CURLMOPT_QUICK_EXIT (21) with CURLM_UNKNOWN_OPTION
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr243Tests.cs, Libcurl/Multi/Settings/CurlMultiOption.cs, Libcurl/Multi/Settings/MultiOptionSetters.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3772 — Fix CGR-243: curl_multi_setopt rejects CURLMOPT_RESOLVE_THREADS_MAX (20) and CURLMOPT_QUICK_EXIT (21) with CURLM_UNKNOWN_OPTION

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-243 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-243: curl_multi_setopt rejects CURLMOPT_RESOLVE_THREADS_MAX (20) and CURLMOPT_QUICK_EXIT (21) with CURLM_UNKNOWN_OPTION

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Windows. Detail: Windows. REF-LINUX 8.18 predates these options and also returns 6. The port code is platform-independent.
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-10`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe multiids` (or the `multi_setopt` section of `probe.exe repro`). It calls `curl_multi_setopt(m, 20, 1L)` and `curl_multi_setopt(m, 21, 1L)`.

Commands (run each with REF and with PORT):

```sh
probe.exe multiids
probe.exe repro
```

**Expected (real curl)**

- both return 0.

**Actual (Curl)**

- both return 6. The `CurlMultiOption` enum has no member for 20 or 21.

**Suspected location in source**

- `Libcurl\Multi\Settings\CurlMultiOption.cs` and `MultiOptionSetters.cs`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr243Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-243 of the third-party gap report.
