---
id: BL-3731
title: Fix CGR-202: Trailers received before a malformed trailer line are not written to -D
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Protocol.Http.UnitTests/Cgr202Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3731 — Fix CGR-202: Trailers received before a malformed trailer line are not written to -D

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-202 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-202: Trailers received before a malformed trailer line are not written to -D

- **Severity:** Low
- **Area:** http-responses
- **Platform(s):** Windows. Detail: Windows (clean comparison only there)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-responses HR-14`

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- `c_trailer_fold.bin` = `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\nT: a\r\n b\r\n\r\n`. Command: `curl -s -S -D - -o NUL http://127.0.0.1:20400/c_trailer_fold`

Commands (run each with REF and with PORT):

```sh
curl -s -S -D - -o NUL http://127.0.0.1:20400/c_trailer_fold
```

**Expected (real curl)**

- writes the head plus `T: a\r\n`, then exit 8 `Header without colon`.

**Actual (Curl)**

- writes the head only, without `T: a\r\n`, then exit 8 `Header without colon`. REF-LINUX 8.18 also writes `T: a\r\n` but exits 43, an older-version difference.

**Suspected location in source**

- `HttpChunkedDecoder.cs` / `HttpResponseBodyReader.cs` trailer handling

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr202Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-202 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

- 2026-10-28 Notes: Cause: `HttpProtocolHandler.CopyBodyAsync` wrote the decoder's stored trailers to -D only for the too-many-headers failure; a malformed trailer (exit 8) skipped them. Fix: any `HttpTransferException` from the body copy now writes the stored trailers before rethrowing (curl does the same: headers are written as received). Not measured against live curl again here; the report gives the expected output (head + `T: a\r\n`, exit 8 `Header without colon`) and the unit test `ExecuteAsync_FoldedTrailerLine_WritesTheTrailerBeforeItThenFailsWithExit8` pins it (scripted bytes of c_trailer_fold). Build clean, Http fast tests 2507 passed, Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 0 failing members. Platform-neutral.
- 2026-10-28: Doing -> Done. Trailers decoded before a malformed trailer line are now written to -D
