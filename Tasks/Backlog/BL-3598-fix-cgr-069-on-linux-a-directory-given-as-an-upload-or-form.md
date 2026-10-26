---
id: BL-3598
title: Fix CGR-069: On Linux, a directory given as an upload or form file (-T dir, -F f=@dir) fails with exit 26 where curl uploads an empty body and exits 0...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests/Cgr069Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3598 — Fix CGR-069: On Linux, a directory given as an upload or form file (-T dir, -F f=@dir) fails with exit 26 where curl uploads an empty body and exits 0...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-069 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-069: On Linux, a directory given as an upload or form file (-T dir, -F f=@dir) fails with exit 26 where curl uploads an empty body and exits 0. -d @dir prints an extra 'Failed to open' line.

- **Severity:** Medium
- **Area:** http-requests
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable. Reproduced 2/2, but REF-LINUX is 8.18.0. On Windows all three cases match REF 8.22.0, where opening a directory fails, so the port copies the Windows behaviour onto Linux, where opening a directory succeeds.
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-requests HR-9`

**Reproduction**

- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- WSL: run `mkdir sub`, then:
  - `curl -sS -T sub http://127.0.0.1:20355/a`
  - `curl -sS -F f=@sub http://127.0.0.1:20355/a`
  - `curl -sS -d @sub http://127.0.0.1:20355/a`

  These are cases v9_*.

Commands (run each with REF and with PORT):

```sh
mkdir sub
curl -sS -T sub http://127.0.0.1:20355/a
curl -sS -F f=@sub http://127.0.0.1:20355/a
curl -sS -d @sub http://127.0.0.1:20355/a
```

**Expected (real curl)**

- *REF behaviour (8.18.0):* 
  - `-T sub` sends `PUT /a` chunked with only `0\r\n\r\n` and exits 0.
  - `-F f=@sub` sends a complete multipart body with an empty part (`filename="sub"`) and exits 0.
  - `-d @sub` exits 26 with two lines: `curl: option -d: error encountered when reading a file` and the try-help line.

**Actual (Curl)**

- `-T sub` sends nothing and exits 26 with `curl: cannot open 'sub'`, the try-help line and `curl: (26) Failed to open/read local data from file/application`.
- `-F f=@sub` sends nothing and exits 26 with `curl: (26) read error getting mime data`.
- `-d @sub` prints the extra first line `curl: Failed to open sub`.

**Suspected location in source**

- the upload and data file readers in `Curl.Cli.UnitLibrary` (`DiskDataFileReader` and the upload path) refuse a directory before the transfer starts. The extra line comes from `CommandLineRefusal.cs` (around line 410). Exact lines not checked.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr069Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-069 of the third-party gap report.
