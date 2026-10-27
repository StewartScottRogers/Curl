---
id: BL-3653
title: Fix CGR-124: CURLOPT_UPLOAD_FLAGS is missing: setopt returns CURLE_NOT_BUILT_IN
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr124Tests.cs, Libcurl/Options/EasyOptionDispatch.cs, Libcurl/Options/Protocols/EasyProtocolOptionSetters.cs, Curl.Api.UnitLibrary/Options/Protocols/EasySmtpOptions.cs, Curl.Api.UnitTests/Options/Protocols/EasySmtpOptionsTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3653 — Fix CGR-124: CURLOPT_UPLOAD_FLAGS is missing: setopt returns CURLE_NOT_BUILT_IN

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-124 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-124: CURLOPT_UPLOAD_FLAGS is missing: setopt returns CURLE_NOT_BUILT_IN

- **Severity:** Medium
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-4`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro`. It calls `curl_easy_setopt(h, CURLOPT_UPLOAD_FLAGS /*327*/, 1L)`. The full value sweep is `probe.exe sweep`.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
probe.exe sweep
```

**Expected (real curl)**

- returns 0 for every value. 8.18 on Linux also returns 0.

**Actual (Curl)**

- returns 4 for every value. The port's own curl tool supports `--upload-flags` (`Curl.Cli` `LibcurlSourceCode.ProtocolOptions.cs:188` emits CURLOPT_UPLOAD_FLAGS), but Libcurl has no setter for it. Only the setopt level was checked; the IMAP APPEND effect was not tested.

**Suspected location in source**

- `Libcurl\Options\EasyOptionDispatch.cs:40`. No `CurlOption.UploadFlags` setter row exists, so the NotBuiltIn fallback applies.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr124Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.- Repro (2026-10-26): REF measured with a C# file-based P/Invoke probe (`dotnet run probe.cs`) against
  `C:\Program Files\Git\ucrt64\bin\libcurl-4.dll` (libcurl/8.22.0 Schannel): `curl_easy_setopt(h, 327, v)` for
  v = 0, 1, 2, 4, 8, 16, 31, 32, 255, 256, -1 returned 0 for every value. PORT before the fix: 4 (`NotBuiltIn`) for
  every value (no setter row). PORT after: `Cgr124Tests` pins `Ok` for 0, 1, 31, 255, 256, -1. The probe has no
  callbacks, CURLINFO reads or wire bytes (setopt only), so those match trivially; Linux 8.18 also returns 0 per the report.
- Fix: a `CURLOPT_UPLOAD_FLAGS` row in `Libcurl/Options/Protocols/EasyProtocolOptionSetters.cs` storing the value's low
  byte in a new `EasySmtpOptions.UploadFlags`, as `setopt.c` does (`s->upload_flags = (unsigned char)arg`), default
  `CURLULFLAG_SEEN` (16), libcurl's default and the curl tool's starting set. No ADR: this matches upstream, no choice
  beyond that. `EasyOptionDispatch.cs` needed no change.
- Touches widened to `EasyProtocolOptionSetters.cs`, `EasySmtpOptions.cs` and its test file
  `Curl.Api.UnitTests/Options/Protocols/EasySmtpOptionsTests.cs` (which counts the record's properties); no task in
  Doing on origin/work/dark-factory named them.
- Follow-up: BL-3905 sends the stored flags in a Libcurl IMAP `APPEND` (the engine's `TransferSettingsOf` still passes
  `UploadFlags: []`); the issue checked only the setopt level.
- Gates: `dotnet build` clean; fast tests all green (Libcurl.UnitTests 1438 passed, Curl.Api.UnitTests 901 passed);
  `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Api.UnitLibrary`: 0 failing members. The change has no paths or
  OS-dependent text, so it is platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-124 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. curl_easy_setopt CURLOPT_UPLOAD_FLAGS answers CURLE_OK for every value and stores its low byte, as libcurl 8.22.0 does
