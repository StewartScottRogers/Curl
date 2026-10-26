---
id: BL-3791
title: Fix CGR-262: HTTPPOST (curl_formadd) with a missing CURLFORM_FILE fails before connecting and fills the error buffer; libcurl connects first and leave...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr262Tests.cs, Libcurl/Mime/Form/CurlForm.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3791 — Fix CGR-262: HTTPPOST (curl_formadd) with a missing CURLFORM_FILE fails before connecting and fills the error buffer; libcurl connects first and leave...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-262 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-262: HTTPPOST (curl_formadd) with a missing CURLFORM_FILE fails before connecting and fills the error buffer; libcurl connects first and leaves the error buffer empty

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-29`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe form`. It calls `curl_formadd(CURLFORM_COPYNAME "f", CURLFORM_FILE "<missing path>")`, sets CURLOPT_HTTPPOST and performs against 127.0.0.1:21411.

Commands (run each with REF and with PORT):

```sh
probe.exe form
```

**Expected (real curl)**

- the return code matches. The server sees 1 connection and ERRORBUFFER is ''.

**Actual (Curl)**

- the return code matches. There are 0 connections, and ERRORBUFFER is `Failed to open/read local data from file/application`.

**Suspected location in source**

- `Libcurl\Mime\Form\CurlForm.cs` and the engine form builder.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr262Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-262 of the third-party gap report.
