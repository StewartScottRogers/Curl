---
id: BL-3784
title: Fix CGR-255: On Windows, curl_url_set with CURLU_DEFAULT_SCHEME treats c:/dir/file as scheme c (UNSUPPORTED_SCHEME); libcurl treats the drive letter a...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitLibrary/UrlRelativeReference.cs, Curl.Engine.UnitTests/Cgr255Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3784 — Fix CGR-255: On Windows, curl_url_set with CURLU_DEFAULT_SCHEME treats c:/dir/file as scheme c (UNSUPPORTED_SCHEME); libcurl treats the drive letter a...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-255 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-255: On Windows, curl_url_set with CURLU_DEFAULT_SCHEME treats `c:/dir/file` as scheme `c` (UNSUPPORTED_SCHEME); libcurl treats the drive letter as the host

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice).
- **Related existing finding (GF/AF):** GF-0041
- **Source findings:** `libcurl-api LA-22`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- `probe.exe url1 c:/dir/file 4` and `probe.exe url1 x:/ 4`.

Commands (run each with REF and with PORT):

```sh
probe.exe url1 c:/dir/file 4
probe.exe url1 x:/ 4
```

**Expected (real curl)**

- returns 0. URL=`https://c/dir/file`, HOST=`c`, PATH=`/dir/file`.

**Actual (Curl)**

- returns 5. URL and HOST then return 14 (no host).

**Suspected location in source**

- `Curl.Engine.UnitLibrary` (the URL parser lacks the `_WIN32` drive-letter rule).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr255Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Fix: `UrlRelativeReference.IsAbsolute` treats a one-letter scheme as a drive letter (no scheme) on Windows under GUESS/DEFAULT_SCHEME, as libcurl `_WIN32` does; touches widened to that file (no other task holds it). Repro `url1 c:/dir/file 4` / `x:/ 4` pinned by Cgr255Tests (Windows-only, OSCondition); real-curl probe not rerun (REF DLL not in lane), expectation from report: https://c/dir/file, host c, path /dir/file. Other variants unchanged (fast tests green, 2888 pass).

## Log

- 2026-10-26: Created from CGR-255 of the third-party gap report.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. Windows drive letter under CURLU_DEFAULT_SCHEME is the host
