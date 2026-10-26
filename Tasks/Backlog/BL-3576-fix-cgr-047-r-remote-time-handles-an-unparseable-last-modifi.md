---
id: BL-3576
title: Fix CGR-047: -R/--remote-time handles an unparseable Last-Modified date, and 1969-12-31 23:59:59, the opposite way from curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlDateParser.cs, Curl.Protocol.Abstractions.UnitTests/Cgr047Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpLastModified.cs, Curl.Protocol.Http.UnitTests/Cgr047Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3576 — Fix CGR-047: -R/--remote-time handles an unparseable Last-Modified date, and 1969-12-31 23:59:59, the opposite way from curl

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-047 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-047: -R/--remote-time handles an unparseable Last-Modified date, and 1969-12-31 23:59:59, the opposite way from curl

- **Severity:** Medium
- **Area:** config-glob-output
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed. The verifier reproduced it on Windows and Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `config-glob-output CGO-02`

**Reproduction**

- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- check the mtime of `f.txt` after each command.
  - `curl -sS -R -O "$U/f.txt?lm=garbage"`
  - `curl -sS -R -O "$U/f.txt?lm=Wed,%2032%20Oct%202015%2007:28:00%20GMT"`
  - `curl -sS -R -O "$U/f.txt?lm=Wed,%2031%20Dec%201969%2023:59:59%20GMT"`
  - The original tester also used the values `''`, `x`, `1445412480`, `2015-10-21T07:28:00Z` and `Wed, 21 Oct 2015 25:28:00 GMT`.
  - Scripts: `E\verify\v2_misc.ps1` r1-r4 and `vl.sh` r1/r3. The original is `E\b_rt.ps1`.

Commands (run each with REF and with PORT):

```sh
curl -sS -R -O "$U/f.txt?lm=garbage"
curl -sS -R -O "$U/f.txt?lm=Wed,%2032%20Oct%202015%2007:28:00%20GMT"
curl -sS -R -O "$U/f.txt?lm=Wed,%2031%20Dec%201969%2023:59:59%20GMT"
E\verify\v2_misc.ps1
E\b_rt.ps1
```

**Expected (real curl)**

- Windows 8.22.0 and Linux 8.18.0 agree.
  - An unparseable value sets the mtime to 1970-01-01T00:00:00Z.
  - `Wed, 31 Dec 1969 23:59:59 GMT` leaves the mtime at the download time.

**Actual (Curl)**

- An unparseable value leaves the mtime at the download time.
- `Wed, 31 Dec 1969 23:59:59 GMT` sets the mtime to 1970-01-01T00:00:00Z.
- Dates that parse normally match REF, for example 2015-10-21 07:28:00.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpLastModified.cs:31-36` returns null (meaning "unknown") when the parse fails.
- `Curl.Protocol.Abstractions.UnitLibrary/CurlDateParser.cs` (around line 149) treats -1 as a real time.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr047Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-047 of the third-party gap report.
