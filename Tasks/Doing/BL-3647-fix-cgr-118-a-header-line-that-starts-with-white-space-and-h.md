---
id: BL-3647
title: Fix CGR-118: A header line that starts with white space and has no previous header to fold into loses its leading blanks in -i/-D output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadBuilder.cs, Curl.Protocol.Http.UnitTests/Cgr118Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3647 — Fix CGR-118: A header line that starts with white space and has no previous header to fold into loses its leading blanks in -i/-D output

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-118 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-118: A header line that starts with white space and has no previous header to fold into loses its leading blanks in `-i`/`-D` output

- **Severity:** Medium
- **Area:** upstream-cases, http-responses
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [upstream-cases UC-3] Confirmed (on all four binaries).
- **Related existing finding (GF/AF):** GF-0012
- **Source findings:** `upstream-cases UC-3`, `http-responses HR-13`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- **[upstream-cases UC-3]** A header line that starts with white space and has no header before it to fold into loses its leading blanks in -i and -D output
  - Server reply `HTTP/1.1 200 OK\r\n Date: Tue, 09 Nov 2010 14:49:00 GMT\r\nServer: x\r\nContent-Length: 6\r\n\r\n-foo-\n` (`verify\conf\fold1.json`). Run `curl -s -i -D hdr.txt http://127.0.0.1:21352/1473`.
  - Tab variant: `\tX-A: one` (`fold2.json`). Script: `verify\v1.sh`.
- **[http-responses HR-13]** Leading whitespace on the first header line is stripped from -i/-D output
  - `fold_first.bin` = `HTTP/1.1 200 OK\r\n  X-Fold: a\r\nContent-Length: 2\r\n\r\nok`. Command: `curl -s -S -i http://127.0.0.1:20400/fold_first`
  - `fold_first2` (` X-Fold: a\r\n  b`)
  - `fold_first3` (`\tX: a`), run with `-D -`

Commands (run each with REF and with PORT):

```sh
# [upstream-cases UC-3]
curl -s -i -D hdr.txt http://127.0.0.1:21352/1473
# [http-responses HR-13]
curl -s -S -i http://127.0.0.1:20400/fold_first
```

**Expected (real curl)**

- **[upstream-cases UC-3]** stdout and hdr.txt hold ` Date: Tue, 09 Nov 2010 14:49:00 GMT` (or `\tX-A: one`) with the leading white space kept.
- **[http-responses HR-13]** writes the line as received: `  X-Fold: a\r\n`, ` X-Fold: a b\r\n`, `\tX: a\r\n`.

**Actual (Curl)**

- **[upstream-cases UC-3]** stdout and hdr.txt hold `Date: ...` (or `X-A: one`) with the leading white space removed. The exit code (0) is the same.
- **[http-responses HR-13]** writes `X-Fold: a\r\n` and the others with the leading blanks removed. Exit codes match. Same on Linux.

**Suspected location in source**

- **[upstream-cases UC-3]** `Curl.Protocol.Http.UnitLibrary\HttpResponseHeadBuilder.cs:89` (`line.Content.TrimStart([' ', '\t'])`; the trimmed text is what gets written out).
- **[http-responses HR-13]** `Curl.Protocol.Http.UnitLibrary\HttpResponseHeadBuilder.cs:89` (`line.Content.TrimStart([' ', '\t'])`)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr118Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-118 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
