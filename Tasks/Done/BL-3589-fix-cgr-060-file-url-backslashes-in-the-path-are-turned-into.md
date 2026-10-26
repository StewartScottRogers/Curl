---
id: BL-3589
title: Fix CGR-060: file:// URL: backslashes in the path are turned into /
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitTests/Cgr060Tests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlFileUrlApiTests.cs, Curl.Protocol.File.UnitTests/FileUrlPathTests.cs, Documentation/Planning/Decisions/ADR-0010-representing-urls-system-uri-cannot-round-trip.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3589 — Fix CGR-060: file:// URL: backslashes in the path are turned into /

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-060 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-060: `file://` URL: backslashes in the path are turned into `/`

- **Severity:** Medium
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0066
- **Source findings:** `url-parsing UP-18`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *, Windows:* `curl -sS "file:///c:\nonexist\zz.txt" -w "[%{url_effective}]"` (v18_file_bs)
- *, Linux:* `curl -sS 'file:///tmp/no\such\file' -w '[%{url_effective}]'` (lv18_file_bs, in `vcases2.py`)

Commands (run each with REF and with PORT):

```sh
curl -sS "file:///c:\nonexist\zz.txt" -w "[%{url_effective}]"
curl -sS 'file:///tmp/no\such\file' -w '[%{url_effective}]'
```

**Expected (real curl)**

- Windows: `curl: (37) Could not open file c:\nonexist\zz.txt`, and `[file://c:\nonexist\zz.txt]`.
- Linux: tries to open `/tmp/no\such\file`.

**Actual (Curl)**

- Windows: `Could not open file c:/nonexist/zz.txt`, and `[file://c:/nonexist/zz.txt]`.
- Linux: tries to open `/tmp/no/such/file`, so a file name that contains a backslash cannot be read.

**Suspected location in source**

- `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs:174` (ParseFile) and `:250` (ConvertBackslashesAfterDoubleSlash).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr060Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Added `CurlUrlTests.cs`, `CurlUrlFileUrlApiTests.cs`, `FileUrlPathTests.cs` and ADR-0010 to `touches`: their tests and text pinned the old conversion. No task in Doing on `origin/work/dark-factory` names them. `CurlUrl.cs` (which passes `keepFileBackslashes: true`) is held by BL-3851, so it is left alone; the now inert parameter is discarded in `CurlUrlParser.Parse` and BL-3865 (depends on BL-3851) removes it.
- Decision (ADR-0822): a `file` URL's backslashes stay as typed, are no separator for dot segments, and `file://\...` begins an authority curl rejects. `ConvertBackslashesAfterDoubleSlash` removed.
- Measured 2026-10-26, `curl -sS <url> -w "[%{url_effective}]"`, real curl = curl 8.21.0 Schannel (`C:\windows\system32\curl.exe`) and 8.18.0 mingw (same answer from both); Curl = `dotnet run --project Curl.Console --no-build --` after the fix:
  - `file:///c:\nonexist\zz.txt`: curl `curl: (37) Could not open file c:\nonexist\zz.txt`, stdout `[file://c:\nonexist\zz.txt]`, exit 37. Curl: identical (before: `c:/nonexist/zz.txt`, `[file://c:/nonexist/zz.txt]`).
  - `file:///tmp/no\such\file`: curl `curl: (37) Could not open file /tmp/no\such\file`, `[file:///tmp/no\such\file]`, exit 37. Curl: identical. The Linux repro opens the same path; the Linux run is pinned with `driveLetters: false` in `Cgr060Tests` (platform-neutral), CI covers Linux and macOS.
  - `file://\server\share`: curl `curl: (3) URL rejected: Bad file:// URL`, `[file://\server\share]`, exit 3. Curl: identical.
  - No files are written and no bytes go on the wire for a `file` URL.
- Already-matching variants still match: `file://c:\...` and `file:/c:\...` (exit 37, backslashes kept), `?a\b` query kept. `file:///C:/dir\..\x` now keeps `C:/dir\..\x` as curl quotes it; here `C:\x` is a directory, which Curl lists and 8.21.0 fails with 37 - that is curl 8.22.0's listing (BL-2166), not this issue.
- Focused test: `Cgr060Tests` failed 4 of its rows against the old parser (run with HEAD's `CurlUrlParser.cs`) and passes after.
- Build clean; fast tests: 46 test projects passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary`: 0 failing members.
## Log

- 2026-10-26: Created from CGR-060 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. file:// URLs keep their backslashes as typed, as curl does (CGR-060)
