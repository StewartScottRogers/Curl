---
id: BL-3711
title: Fix CGR-182: --skip-existing -o NUL: REF treats NUL as an existing file and skips the transfer; PORT runs it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests/Cgr182Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3711 — Fix CGR-182: --skip-existing -o NUL: REF treats NUL as an existing file and skips the transfer; PORT runs it

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-182 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-182: --skip-existing -o NUL: REF treats NUL as an existing file and skips the transfer; PORT runs it

- **Severity:** Low
- **Area:** cli-options
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0013
- **Source findings:** `cli-options CLIO-20`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- `curl -sS --skip-existing -o NUL -m 10 http://127.0.0.1:20002/`.

Commands (run each with REF and with PORT):

```sh
curl -sS --skip-existing -o NUL -m 10 http://127.0.0.1:20002/
```

**Expected (real curl)**

- exit 0 and no connect attempt.

**Actual (Curl)**

- exit 7 `Failed to connect ...`.

**Suspected location in source**

- Not located by the reviewers.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr182Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-182 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Result

- Cause: `PhysicalOutputPaths.Exists` used `File.Exists`/`Directory.Exists`, which are false for the Windows NUL device; curl's `stat` finds it. Fix: on Windows `Exists` also finds an exact DOS device name (CON, PRN, AUX, NUL, COM1-9, LPT1-9, any case) via the new `findsDosDevices` constructor flag (default constructor passes `OperatingSystem.IsWindows()`).
- Measured: `curl -sS --skip-existing -o NUL -m 10 http://127.0.0.1:20002/` now exits 0 with no connect attempt, as REF does (before: exit 7).
- Unit tests in `Cgr182Tests.cs` are platform-neutral (flag passed explicitly). CI on Linux/macOS: unaffected, flag is false there.
- Measure-CodeQuality for Curl.Console: 0 failing members. Build clean; fast tests green (3026 passed).
- 2026-10-28: Doing -> Done. NUL now counts as existing on Windows, so --skip-existing -o NUL skips the transfer (exit 0)
