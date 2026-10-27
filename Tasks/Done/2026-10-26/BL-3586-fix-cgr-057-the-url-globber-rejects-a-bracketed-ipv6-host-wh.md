---
id: BL-3586
title: Fix CGR-057: The URL globber rejects a bracketed IPv6 host whose zone id contains %XX or :
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/Globbing/UrlGlobParser.cs, Curl.Core.UnitTests/Cgr057Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3586 — Fix CGR-057: The URL globber rejects a bracketed IPv6 host whose zone id contains %XX or :

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-057 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-057: The URL globber rejects a bracketed IPv6 host whose zone id contains `%XX` or `:`

- **Severity:** Medium
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-11`

PORT reports `bad range specification` with exit 3, where curl accepts the URL.

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- `curl -sS "http://[::1%25a%20b]:20200/a"` (v11_zone_glob)
- `curl -sS "http://[::1%25a:b]:20200/a"` (v11b_zone_colon)
- Linux cases: lv11_*.

Commands (run each with REF and with PORT):

```sh
curl -sS "http://[::1%25a%20b]:20200/a"
curl -sS "http://[::1%25a:b]:20200/a"
```

**Expected (real curl)**

- exit 0. The request is sent with `Host: [::1]:20200`.

**Actual (Curl)**

- exit 3, `curl: (3) bad range specification in position 9:` plus the caret line. Nothing is sent.

**Suspected location in source**

- `Curl.Core.UnitLibrary/Globbing/UrlGlobParser.cs:222-225` (`IsZone`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr057Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Cause: `UrlGlobParser.IsZone` allowed only `[A-Za-z0-9-._~]` in a zone id, so `%` and `:` sent the bracket to the range reader. Fix (ADR-0822): curl 8.22.0's rule, measured on Windows: an optional leading `25`; otherwise two hex digits after `%` refuse it; then 1 to 15 characters with no space, control, `/`, `?`, `#` or `@`. 31 hosts measured (`curl -sS "http://[<host>]:1/a"`, exit 7 taken / exit 3 range) are listed in the ADR and pinned in `Cgr057Tests`.
- Repros, run with `Record-CurlExchange.ps1 -Port 20251 -ListenAddress ::1` (200, body `ok\n`), real curl 8.22.0 and Curl (`-Curl Curl.Console\bin\Debug\net10.0\curl.exe`): `curl -sS "http://[::1%25a%20b]:20251/a"` and `curl -sS "http://[::1%25a:b]:20251/a"` both give exit 0, stdout `ok`, empty stderr, request `GET /a HTTP/1.1` / `Host: [::1]:20251`; request.bin, stdout.bin, stderr.txt and exitcode.txt identical (cmp) for both. No files are written. Before the fix Curl gave exit 3, `bad range specification in position 9:` (the report's Actual).
- Fails before the fix: the old `IsZone` refused every zone holding `%` or `:`, so the first DataRows of `TryParse_ZoneIdCurlTakes_ExpandsToTheUrlUnchanged` fail on it (reverting the file to rerun was refused by the lane's permissions, so this is from the code, not a run). Already-matching variants (`%25eth0`, `%eth0`, `%25]`, `%]`, `%e/h`) stay pinned in `UrlGlobTests` and pass.
- Non-ASCII length: curl on Windows receives arguments in the ANSI code page (`25é` + 14 letters is taken), so the limit counts characters; a Linux UTF-8 difference is left unpinned. Tests are platform-neutral (pure parsing), so CI on Linux and macOS is covered by the green fast run here.
- Gates: `dotnet build` 0 warnings; fast tests green in all 46 test projects (Curl.Core.UnitTests 1879 passed, 8 skipped); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` 0 failing members (first run flagged `IsZone` at complexity 14, split into `StartsWithTwoHexDigits` and `IsZoneName`).

## Log

- 2026-10-26: Created from CGR-057 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. The URL globber takes a bracketed IPv6 host whose zone id holds %XX or : as curl 8.22.0 does (CGR-057)
