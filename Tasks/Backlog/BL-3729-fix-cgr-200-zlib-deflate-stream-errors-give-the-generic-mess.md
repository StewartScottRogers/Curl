---
id: BL-3729
title: Fix CGR-200: zlib/deflate stream errors give the generic message instead of the zlib message
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr200Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3729 — Fix CGR-200: zlib/deflate stream errors give the generic message instead of the zlib message

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-200 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-200: zlib/deflate stream errors give the generic message instead of the zlib message

- **Severity:** Low
- **Area:** http-responses
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-responses HR-6`

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- `z_deflate_garbage.bin` = `HTTP/1.1 200 OK\r\nContent-Encoding: deflate\r\nContent-Length: 29\r\n\r\nthis is not compressed at all`. Command: `curl --compressed -s -S http://127.0.0.1:20400/z_deflate_garbage`

Commands (run each with REF and with PORT):

```sh
curl --compressed -s -S http://127.0.0.1:20400/z_deflate_garbage
```

**Expected (real curl)**

- `curl: (61) Error while processing content unencoding: invalid code lengths set`

**Actual (Curl)**

- `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding`. The exit code is the same. Only a few zlib messages are mapped.

**Suspected location in source**

- `HttpContentCodingDecoder.cs:385`, `HttpTransferMessages.cs:333-359`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr200Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-200 of the third-party gap report.
