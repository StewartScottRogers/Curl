---
id: BL-3603
title: Fix CGR-074: Non-ASCII bytes in a .netrc login or password are re-encoded instead of sent as the file's bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/NetrcFile.cs, Curl.Authentication.UnitTests/Cgr074Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3603 — Fix CGR-074: Non-ASCII bytes in a .netrc login or password are re-encoded instead of sent as the file's bytes

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-074 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-074: Non-ASCII bytes in a .netrc login or password are re-encoded instead of sent as the file's bytes

- **Severity:** Medium
- **Area:** http-stateful
- **Platform(s):** Windows. Detail: Windows (Confirmed); Linux Probable
- **Confidence:** Confirmed. Detail: Confirmed on Windows
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-stateful HS-6`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- n.txt = `machine 127.0.0.1 login us\xE9r password p\xE4ss` (Latin-1 bytes), or the same text saved as UTF-8 (`c3 a9` / `c3 a4`).
- Command: `curl -sS --netrc-file n.txt http://127.0.0.1:20500/p`.
- Case: `EV\cases\nr-utf8` (the Latin-1 file).

Commands (run each with REF and with PORT):

```sh
curl -sS --netrc-file n.txt http://127.0.0.1:20500/p
```

**Expected (real curl)**

- *REF (Windows 8.22):* sends the file's bytes unchanged. Latin-1 file: `Authorization: Basic dXPpcjpw5HNz`. UTF-8 file: `Basic dXPDqXI6cMOkc3M=`.

**Actual (Curl)**

- *PORT (Windows):* Latin-1 file: `Basic dXM/cjpwP3Nz` (`us?r:p?ss`). UTF-8 file: `Basic dXPpcjpw5HNz` (re-encoded to cp1252).

**Notes**

- *Linux:* 
  - PORT sends `dXPvv71yOnDvv71zcw==` (U+FFFD replacement characters) for the Latin-1 file and the unchanged UTF-8 bytes for the UTF-8 file.
  - REF-LINUX 8.18 is itself broken here. It sends `us:p` for the Latin-1 file and fails with `(26) .netrc error: syntax error` for the UTF-8 file.
  - So Linux cannot be judged against 8.18 (see `EV\linux\vnetrc.sh`).

**Suspected location in source**

- `Curl.Authentication.UnitLibrary/NetrcFile.cs:110-114` decodes the file as UTF-8. The credential is then re-encoded for the platform (the ANSI code page on Windows).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr074Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-074 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
