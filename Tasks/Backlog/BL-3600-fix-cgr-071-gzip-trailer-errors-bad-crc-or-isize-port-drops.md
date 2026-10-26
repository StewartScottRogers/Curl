---
id: BL-3600
title: Fix CGR-071: gzip trailer errors (bad CRC or ISIZE): PORT drops all decoded data and gives a generic error message
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs, Curl.Protocol.Http.UnitTests/Cgr071Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3600 — Fix CGR-071: gzip trailer errors (bad CRC or ISIZE): PORT drops all decoded data and gives a generic error message

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-071 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-071: gzip trailer errors (bad CRC or ISIZE): PORT drops all decoded data and gives a generic error message

- **Severity:** Medium
- **Area:** http-responses
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-responses HR-5`

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- `z_gzip_badcrc.bin` = `HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 58\r\n\r\n` + a valid gzip of `hello compressed world\n` x50 (1150 bytes decoded) with one CRC32 trailer byte flipped.
- `z_gzip_badlen` flips an ISIZE byte instead.
- Generator: `EV\gen\gen.cs`. Case builder: `EV\cases3.ps1`.
- Command: `curl --compressed -s -S http://127.0.0.1:20400/z_gzip_badcrc`

Commands (run each with REF and with PORT):

```sh
EV\cases3.ps1
curl --compressed -s -S http://127.0.0.1:20400/z_gzip_badcrc
```

**Expected (real curl)**

- writes all 1150 decoded bytes, then exits 61 with `curl: (61) Error while processing content unencoding: incorrect data check` (or `incorrect length check`).

**Actual (Curl)**

- writes 0 bytes and exits 61 with `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding`. Same on Linux.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\HttpContentCodingDecoder.cs` `ReadDecoded` (~379-388). .NET `GZipStream` throws `InvalidDataException` before the buffered output is delivered.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr071Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-071 of the third-party gap report.
