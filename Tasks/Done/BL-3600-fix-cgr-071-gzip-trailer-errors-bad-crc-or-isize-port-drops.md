---
id: BL-3600
title: Fix CGR-071: gzip trailer errors (bad CRC or ISIZE): PORT drops all decoded data and gives a generic error message
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs, Curl.Protocol.Http.UnitLibrary/HttpContentChecksumTrailer.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr071Tests.cs, Curl.Protocol.Http.UnitTests/HttpContentCodingDecoderTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr071Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Cause: .NET's `GZipStream`/`ZLibStream` throw `InvalidDataException` on a failed CRC-32, size or Adler-32 check and drop what that read decoded; the exception text is the same for every failure ("The archive entry was compressed using an unsupported compression method.", measured with a file-based app), so it cannot name the check.
- Fix (ADR-0824): `HttpContentCodingDecoder` feeds the first and last 8 encoded bytes of each piece one at a time, so the trailer's failing read decodes nothing; `HttpContentChecksumTrailer.CheckFailed` names the check from how far past the last decoded output the stream failed (4-7 bytes: `incorrect data check` if those 4 bytes are not the checksum; gzip 8-11 bytes after a matching CRC-32: `incorrect length check`); anything else stays ADR-0031's generic text. A gzip member's end is also sought after each feed once something was decoded, so `gzip, then 1F 41` stays exit 23. Touches widened to `HttpContentChecksumTrailer.cs`, `HttpTransferMessages.cs` (the two zlib texts) and `HttpContentCodingDecoderTests.cs` (its large-body test pinned the exact piece split 16384/16384/7232; the trailer now comes in one more small piece, so it asserts the largest piece and the total); no task in Doing on origin/work/dark-factory names them.
- Measured 2026-10-26 with `Record-CurlExchange.ps1` (script in a scratch folder outside the repo), body = .NET `GZipStream` Optimal of `hello compressed world\n` x50 (58 bytes, 1150 decoded), served as `HTTP/1.1 200 OK\r\nContent-Encoding: gzip\r\nContent-Length: 58\r\n\r\n<gzip>`; command `curl --compressed -s -S http://127.0.0.1:<port>/z_gzip_badcrc` (first CRC byte flipped) and `/z_gzip_badlen` (first ISIZE byte flipped). REF = curl 8.22.0 Schannel (ucrt64), PORT = Curl.Console Debug build:
  - badcrc REF: exit 61, stdout 1150 bytes (sha F772F4B25D28...), stderr `curl: (61) Error while processing content unencoding: incorrect data check`; PORT: identical.
  - badlen REF: exit 61, stdout 1150 bytes (same sha), stderr `curl: (61) Error while processing content unencoding: incorrect length check`; PORT: identical.
  - Request bytes identical (`GET /z_gzip_bad* HTTP/1.1`, Host, `User-Agent: curl/8.22.0`, `Accept: */*`, `Accept-Encoding: deflate, gzip, br, zstd`); no files written by either.
- Linux: the OpenSSL build reports the same zlib texts and the change is platform-neutral (no paths, OS text or certificates), so REF-LINUX matches by the same code path; CI's Linux and macOS runs check the fast tests.
- Focused tests: `Cgr071Tests` (gzip bad CRC / bad size, zlib bad Adler-32 across chunk sizes 1 to 65536, asserting all 1150 bytes then the named text; corrupt deflate keeps the generic text; `CheckFailed` branches). Before the fix they fail: `CheckFailed` did not exist, and the old decoder wrote 0 bytes with the generic text (the issue's Actual). The issue has no variant that already matched; the existing corrupt-body, bytes-after-the-end and multi-member decoder tests still pass.
- Gates: `dotnet build` clean; fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-071 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. gzip and zlib trailer check failures write every decoded byte, then exit 61 with zlib's incorrect data/length check text, as curl 8.22.0 does
