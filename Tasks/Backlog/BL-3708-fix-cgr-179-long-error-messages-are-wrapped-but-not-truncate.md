---
id: BL-3708
title: Fix CGR-179: Long error messages are wrapped but not truncated like REF's; --dump-header open failure is not wrapped
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/WrappedMessage.cs, Curl.Cli.UnitTests/Cgr179Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3708 — Fix CGR-179: Long error messages are wrapped but not truncated like REF's; --dump-header open failure is not wrapped

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-179 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-179: Long error messages are wrapped but not truncated like REF's; --dump-header open failure is not wrapped

- **Severity:** Low
- **Area:** cli-options
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-16`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- `L=$(head -c 3000 /dev/zero | tr '\0' x); curl -sS --cacert $L -o NUL http://127.0.0.1:20002/`; `curl -sS --dump-header $L -o NUL http://127.0.0.1:20002/`.

Commands (run each with REF and with PORT):

```sh
L=$(head -c 3000 /dev/zero | tr '\0' x); curl -sS --cacert $L -o NUL http://127.0.0.1:20002/
curl -sS --dump-header $L -o NUL http://127.0.0.1:20002/
```

**Expected (real curl)**

- `--cacert` gives 17 lines (truncated at about 1 KB). `--dump-header` gives 16 wrapped lines.

**Actual (Curl)**

- `--cacert` gives 45 lines (not truncated). `--dump-header` gives a single unwrapped line of about 3000 characters.

**Suspected location in source**

- `Curl.Cli.UnitLibrary/WrappedMessage.cs:59` (no size cap).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr179Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-179 of the third-party gap report.
