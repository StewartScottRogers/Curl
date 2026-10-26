---
id: BL-3655
title: Fix CGR-126: CURLINFO_SIZE_DELIVERED returns the raw downloaded byte count (compressed size with ACCEPT_ENCODING, header bytes left out with CURLOPT_H...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr126Tests.cs, Libcurl/Info/Times/EasyInfoReaders.Times.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3655 — Fix CGR-126: CURLINFO_SIZE_DELIVERED returns the raw downloaded byte count (compressed size with ACCEPT_ENCODING, header bytes left out with CURLOPT_H...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-126 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-126: CURLINFO_SIZE_DELIVERED returns the raw downloaded byte count (compressed size with ACCEPT_ENCODING, header bytes left out with CURLOPT_HEADER)

- **Severity:** Medium
- **Area:** libcurl-api
- **Platform(s):** Windows. Detail: Windows confirmed. REF-LINUX 8.18 has no SIZE_DELIVERED (it returns 48). The port code is platform-independent and gives the same values on Linux.
- **Confidence:** Confirmed. Detail: Confirmed (Windows twice).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-6`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe repro`.
  - Case 1: ACCEPT_ENCODING `""`, against a `Content-Encoding: gzip` response with a 25-byte body that decodes to `hello`.
  - Case 2: CURLOPT_HEADER=1, against a 200 response with a 5-byte body.

Commands (run each with REF and with PORT):

```sh
probe.exe repro
```

**Expected (real curl)**

- gzip gives SIZE_DELIVERED=5 (the decoded bytes passed to the write callback). HEADER=1 gives SIZE_DELIVERED=69 (headers plus body).

**Actual (Curl)**

- gzip gives 25 and HEADER=1 gives 5. In both cases the value equals SIZE_DOWNLOAD_T.

**Suspected location in source**

- `Libcurl\Info\Times\EasyInfoReaders.Times.cs:111`. `ReadSizeDelivered` returns `info.SizeDownload`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows (Expected).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr126Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-126 of the third-party gap report.
