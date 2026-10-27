---
id: BL-3654
title: Fix CGR-125: Deprecated no-op options CURLOPT_SSL_ENABLE_NPN, CURLOPT_EGDSOCKET and CURLOPT_RANDOM_FILE return CURLE_NOT_BUILT_IN instead of CURLE_OK
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr125Tests.cs, Libcurl/Options/EasyOptionDispatch.cs, Libcurl/Options/Tls/EasyTlsOptionSetters.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3654 — Fix CGR-125: Deprecated no-op options CURLOPT_SSL_ENABLE_NPN, CURLOPT_EGDSOCKET and CURLOPT_RANDOM_FILE return CURLE_NOT_BUILT_IN instead of CURLE_OK

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-125 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-125: Deprecated no-op options CURLOPT_SSL_ENABLE_NPN, CURLOPT_EGDSOCKET and CURLOPT_RANDOM_FILE return CURLE_NOT_BUILT_IN instead of CURLE_OK

- **Severity:** Medium
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-5`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro` (setopt section): `setopt(SSL_ENABLE_NPN, 0L)`, `setopt(EGDSOCKET, "/tmp/egd")`, `setopt(RANDOM_FILE, "/dev/urandom")`. All values are in `probe.exe sweep`.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
probe.exe sweep
```

**Expected (real curl)**

- all three return 0 (accepted and ignored) for every value.

**Actual (Curl)**

- all three return 4 for every value. Legacy code that checks the result, such as the common `setopt(SSL_ENABLE_NPN, 0L)`, now fails.

**Suspected location in source**

- `Libcurl\Options\EasyOptionDispatch.cs:40` (no setter rows, so the NotBuiltIn fallback applies).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr125Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.- Added `Libcurl/Options/Tls/EasyTlsOptionSetters.cs` to `touches`: the fix is three setter rows there (the TLS group's `AddHandleSetters`), not in `EasyOptionDispatch.cs`, whose `NotBuiltIn` fallback stays right for options no group sets. No other task in Doing on `origin/work/dark-factory` names that file.
- Measured REF with a C# file-based P/Invoke probe over `C:\Program Files\Git\ucrt64\bin\libcurl-4.dll` (`libcurl/8.22.0 Schannel ...`), the setopt section of `probe.exe repro`/`sweep`: `SSL_ENABLE_NPN` 0, 1, 2, -1 -> 0; `EGDSOCKET` and `RANDOM_FILE` "/tmp/egd", "", NULL -> 0. PORT before the fix: 4 (`CURLE_NOT_BUILT_IN`) for every value; after: 0 for every value, the options unchanged (`Cgr125Tests`, which failed before the fix on the dispatch's `NotBuiltIn` fallback). No bytes on the wire, callbacks or CURLINFO values are involved: setopt only.
- Decision: the three options are accepted and ignored with `CURLE_OK` by one `IgnoreDeprecated` setter, the same shape as `CURLOPT_DNS_USE_GLOBAL_CACHE`'s `IgnoreDnsUseGlobalCache`; matching upstream's `setopt.c`, so no ADR. A string past `CURL_MAX_INPUT_LENGTH` still answers `BadFunctionArgument` from the dispatch's length check, as for every string option; left as is.
- The only repro variants are setopt return codes; none matched before, and every other Libcurl test still passes (1448 passed, 1 skipped). Full build clean, fast tests green with no failing project; `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members. The change is platform-neutral (no paths, files or OS text), so CI on Linux and macOS is covered by these runs.

## Log

- 2026-10-26: Created from CGR-125 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Deprecated CURLOPT_SSL_ENABLE_NPN, EGDSOCKET and RANDOM_FILE answer CURLE_OK for every value, as libcurl 8.22.0 does
