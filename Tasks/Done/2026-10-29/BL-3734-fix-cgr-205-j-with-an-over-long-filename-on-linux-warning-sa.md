---
id: BL-3734
title: Fix CGR-205: -J with an over-long filename on Linux: warning says "Invalid argument" instead of "File name too long"
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests/Cgr205Tests.cs, Curl.Core.UnitLibrary/FileSystem, Curl.Core.UnitTests/FileSystem/FileOpenFailureTests.cs, Curl.Protocol.Abstractions.UnitLibrary/FileAccessStatus.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3734 — Fix CGR-205: -J with an over-long filename on Linux: warning says "Invalid argument" instead of "File name too long"

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-205 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-205: -J with an over-long filename on Linux: warning says "Invalid argument" instead of "File name too long"

- **Severity:** Low
- **Area:** http-responses
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: Confirmed (reproduced by the verifier)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-responses HR-17`

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- `j_long.bin` sends `Content-Disposition: attachment; filename="<300 x 'x'>.txt"`. Command (WSL): `curl -OJ http://127.0.0.1:20450/j_long`

Commands (run each with REF and with PORT):

```sh
curl -OJ http://127.0.0.1:20450/j_long
```

**Expected (real curl)**

- `Warning: xxx...txt: File name too long`, then exit 23

**Actual (Curl)**

- `Warning: xxx...txt: Invalid argument`, then exit 23. On Windows both print `Invalid argument`. The text is the OS strerror for open() (ENAMETOOLONG), not curl-version-specific.

**Suspected location in source**

- Curl.Console output-file open error mapping (errno to text)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr205Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-205 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes

- Not measured against real curl in this run (no Linux curl on the lane's Windows host); the report's expected text (`File name too long`, exit 23) and its statement that Windows prints `Invalid argument` are taken as given. Decision: `FileOpenFailure.StatusFor` maps `PathTooLongException` (what .NET throws for ENAMETOOLONG) to a new `FileAccessStatus.NameTooLong`; `OutputFileOpenWarning` words it `File name too long` off Windows and `Invalid argument` on Windows, so Windows output is unchanged. Added Curl.Core and Curl.Protocol.Abstractions files to `touches`: no other Doing task on the shared branch names them.
- Unit tests: `Cgr205Tests` (Console) and `FileOpenFailureTests.StatusFor_PathTooLongException_IsNameTooLong` (Core). Fast tests green for Core, Console, Abstractions; Measure-CodeQuality clean for all three. Linux/macOS CI: change is platform-neutral (both wordings tested on every platform).
- 2026-10-28: Doing -> Done. Over-long name now warns File name too long off Windows
