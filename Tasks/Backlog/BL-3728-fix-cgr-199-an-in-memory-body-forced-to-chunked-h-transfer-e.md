---
id: BL-3728
title: Fix CGR-199: An in-memory body forced to chunked (-H 'Transfer-Encoding: chunked' with -d, --data-binary or --json) goes out as one chunk; curl uses c...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Protocol.Http.UnitTests/Cgr199Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3728 — Fix CGR-199: An in-memory body forced to chunked (-H 'Transfer-Encoding: chunked' with -d, --data-binary or --json) goes out as one chunk; curl uses c...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-199 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-199: An in-memory body forced to chunked (-H 'Transfer-Encoding: chunked' with -d, --data-binary or --json) goes out as one chunk; curl uses chunks of at most 65366 bytes

- **Severity:** Low (After de-chunking the body is identical)
- **Area:** http-requests
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed: reproduced 2/2 on Windows and 2/2 on Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-requests HR-12`

**Reproduction**

- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- Create a 70000-byte b.bin, then run `curl -sS -H "Transfer-Encoding: chunked" --data-binary @b.bin http://127.0.0.1:20335/a` (case v12_te_d_70k). The finder's cases are in `E\c23.json`.

Commands (run each with REF and with PORT):

```sh
curl -sS -H "Transfer-Encoding: chunked" --data-binary @b.bin http://127.0.0.1:20335/a
```

**Expected (real curl)**

- Two chunks with sizes `ff56` and `121a` (65366 + 4634), then `0`.

**Actual (Curl)**

- One chunk of size `11170` (70000), then `0`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs` writes an in-memory body as a single chunk.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr199Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-199 of the third-party gap report.
