---
id: BL-3577
title: Fix CGR-048: --create-dirs through an existing regular file: the port sends the request, then fails with a different error (Windows)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr048Tests.cs, Curl.Console/OutputFileDirectories.cs, Curl.Console/PhysicalOutputPaths.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3577 — Fix CGR-048: --create-dirs through an existing regular file: the port sends the request, then fails with a different error (Windows)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-048 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-048: --create-dirs through an existing regular file: the port sends the request, then fails with a different error (Windows)

- **Severity:** Medium
- **Area:** config-glob-output
- **Platform(s):** Windows. Detail: Windows (Linux matches REF)
- **Confidence:** Confirmed. Detail: Confirmed on Windows. Linux matches REF (`vl.sh` c1).
- **Related existing finding (GF/AF):** none
- **Source findings:** `config-glob-output CGO-04`

**Reproduction**

- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- in an empty directory, create a file named `blk`, then run either command.
  - `curl -sS --create-dirs -o blk/b/c.txt $U/1`
  - `curl -sS --create-dirs --output-dir blk/b -O $U/c.txt`
  - Script: `E\verify\v2_misc.ps1` c1/c2. The original is `E\b_out2.ps1` p44.

Commands (run each with REF and with PORT):

```sh
curl -sS --create-dirs -o blk/b/c.txt $U/1
curl -sS --create-dirs --output-dir blk/b -O $U/c.txt
E\verify\v2_misc.ps1
E\b_out2.ps1
```

**Expected (real curl)**

- No request is sent.
- stderr:
  ```
  curl: Error creating directory blk/b
  curl: (23) Failed writing received data to disk/application
  ```
- Exit 23.

**Actual (Curl)**

- Sends `GET /1`, then prints `curl: (23) client returned ERROR on write of 8 bytes` and exits 23.
- The "Error creating directory" line is missing, and the request still goes on the wire.

**Suspected location in source**

- `Curl.Console/PhysicalOutputPaths.cs:24-44` maps a `Directory.CreateDirectory` failure to an errno. On Windows that errno is one `Curl.Console/OutputFileDirectories.cs:52-56` ignores (EEXIST/EACCES).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr048Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-048 of the third-party gap report.
