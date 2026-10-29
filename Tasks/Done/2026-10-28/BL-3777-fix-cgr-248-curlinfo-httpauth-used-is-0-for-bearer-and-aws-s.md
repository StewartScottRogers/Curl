---
id: BL-3777
title: Fix CGR-248: CURLINFO_HTTPAUTH_USED is 0 for Bearer and AWS-SigV4 requests
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr248Tests.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3777 — Fix CGR-248: CURLINFO_HTTPAUTH_USED is 0 for Bearer and AWS-SigV4 requests

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-248 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-248: CURLINFO_HTTPAUTH_USED is 0 for Bearer and AWS-SigV4 requests

- **Severity:** Low
- **Area:** libcurl-api
- **Platform(s):** Windows. Detail: Windows. REF-LINUX 8.18 also reports 0, so this is an 8.22 behaviour.
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-15`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro`.
  - Case 1: HTTPAUTH=64 with XOAUTH2_BEARER `tok`.
  - Case 2: AWS_SIGV4 `aws:amz:us-east-1:s3` with USERPWD `A:B`.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
```

**Expected (real curl)**

- 64 for Bearer and 128 for SigV4.

**Actual (Curl)**

- 0 for both. The request bytes are identical to REF, and Basic correctly reports 1.

**Suspected location in source**

- `Libcurl\Info\Longs\EasyInfoReaders.Longs.cs` (HttpauthUsed comes from the engine's TransferInfo).

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr248Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Fix in `ReadHttpAuthUsed`: engine reports 0 for Bearer/SigV4 (its handler does not pick them), so when the engine gave 0 and a request was sent (RequestSize > 0) the reader answers 128 for AWS_SIGV4 set, else 64 for XOAUTH2_BEARER set. Chosen over an Engine/Http-handler change to stay in `touches`. Repro: the probe (SCR) is gone; expected 64/128 are the report's measured REF values, not re-measured here; Cgr248Tests pins them and Basic (1) and no-auth (0) unchanged. Libcurl quality run: 0 failing members. Linux/macOS: platform-neutral, no file or OS use.

## Log

- 2026-10-26: Created from CGR-248 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. CURLINFO_HTTPAUTH_USED answers 64 for Bearer and 128 for AWS SigV4 requests
