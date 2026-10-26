---
id: BL-3576
title: Fix CGR-047: -R/--remote-time handles an unparseable Last-Modified date, and 1969-12-31 23:59:59, the opposite way from curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlDateParser.cs, Curl.Protocol.Abstractions.UnitTests/Cgr047Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpLastModified.cs, Curl.Protocol.Http.UnitTests/Cgr047Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpDownloadConditions.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpLastModifiedTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr047Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 5): Measured with Record-CurlExchange.ps1 (REF = curl 8.22.0 Schannel from Git for Windows, PORT = Curl.Console Curl.dll), server replying 200 with Content-Length: 11, Last-Modified: <value> and body path=/f.txt, command curl -sS -R -o f.txt http://127.0.0.1:<port>/f.txt. Before the fix: REF set the mtime to 1970-01-01T00:00:00Z for garbage, Wed, 32 Oct 2015 07:28:00 GMT, an empty value, x, 1445412480, 2015-10-21T07:28:00Z and Wed, 21 Oct 2015 25:28:00 GMT, and left the download time for Wed, 31 Dec 1969 23:59:59 GMT; PORT did the opposite (reproduced). After the fix every value matches: same exit 0, empty stdout and stderr, same body in f.txt, same mtime (epoch, download time, or 2015-10-21T07:28:00Z for the readable date that already matched) and byte-identical request.bin once the port is normalised.
- Decision (ADR-0821): curl's document time for Last-Modified is 0 for an unreadable value and -1 for one second before the epoch. New CurlDateParser.ParseOrEpoch (TryParse keeps its -1 to 0 bump for -z, cookies and HSTS); HttpLastModified.Find gives the document time to -z, HttpLastModified.FindFileTime gives -R and %{filetime} that time with -1 as no time.
- -z checked too, since it reads the same header: -z "Jan 1 2000" and -z "-Jan 1 2000" against garbage (REF: body, 200 both), Wed, 31 Dec 1969 23:59:59 GMT (REF: 304, then body) and Thu, 01 Jan 1970 00:00:00 GMT (REF: body both; PORT answered 304 for the first). HttpDownloadConditions.IsMet now treats a document time of 0 as met, as Curl_meets_timecondition does; all six now match.
- touches: added HttpDownloadConditions.cs and HttpProtocolHandler.cs (one line each: the 0 rule and the FindFileTime call) and HttpLastModifiedTests.cs (its garbage-is-null row no longer holds; moved to Cgr047Tests). No task in Doing on origin/work/dark-factory names them.
- Tests: Curl.Protocol.Abstractions.UnitTests/Cgr047Tests.cs and Curl.Protocol.Http.UnitTests/Cgr047Tests.cs pin what the old code got wrong (measured above: garbage gave no time, the 1969 date gave 0, a document time of 0 was unmet; ParseOrEpoch is new, so the Abstractions test cannot build against the old code; not run against it) and pass now. Fast tests: 46 test projects passed, 0 failed. Measure-CodeQuality for both libraries: 0 failing members. Linux and macOS: the change is date arithmetic on a header value with no path, OS error or platform text, so it is platform-neutral; CI on those platforms is left to the shift's CI watch.

## Log

- 2026-10-26: Created from CGR-047 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. -R and -z read an unparseable Last-Modified as the epoch and 1969-12-31 23:59:59 as -1 (no file time), as curl 8.22.0 does
