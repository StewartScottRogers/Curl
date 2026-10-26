---
id: BL-3595
title: Fix CGR-066: -H 'Name ;' (a blank before the semicolon) sends 'Name :' instead of the trimmed empty header 'Name:'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpCustomHeader.cs, Curl.Protocol.Http.UnitTests/Cgr066Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3595 — Fix CGR-066: -H 'Name ;' (a blank before the semicolon) sends 'Name :' instead of the trimmed empty header 'Name:'

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-066 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-066: -H 'Name ;' (a blank before the semicolon) sends 'Name :' instead of the trimmed empty header 'Name:'

- **Severity:** Medium
- **Area:** http-requests
- **Platform(s):** Windows. Detail: Windows (compared with REF 8.22.0; REF-LINUX 8.18.0 behaves like the port, so this follows an 8.22 change)
- **Confidence:** Confirmed. Detail: Confirmed: reproduced 2/2 on Windows.
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-requests HR-6`

**Reproduction**

- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- Run `curl -sS -H "X-E ;" http://127.0.0.1:20335/a` and `curl -sS -H " X-E;" http://127.0.0.1:20335/a`, cases v6_*. The finder's cases are in `E\c12.json`, which also covers tab, `Accept ;`, an @file line and `--proxy-header`.

Commands (run each with REF and with PORT):

```sh
curl -sS -H "X-E ;" http://127.0.0.1:20335/a
curl -sS -H " X-E;" http://127.0.0.1:20335/a
```

**Expected (real curl)**

- Sends `X-E:\r\n` in both cases.

**Actual (Curl)**

- Sends `X-E :\r\n` and ` X-E:\r\n`. Both are invalid HTTP field lines that servers may reject with 400.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpCustomHeader.cs:60-62`. The semicolon form uses `entry[..semicolon] + ":"` without trimming.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr066Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-066 of the third-party gap report.
