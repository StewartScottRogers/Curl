---
id: BL-3545
title: Fix CGR-016: Linux: invalid UTF-8 bytes in arguments are replaced with U+FFFD before use
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitTests/Cgr016Tests.cs, Curl.Console, Curl.Console.UnitTests/Cgr016Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3545 — Fix CGR-016: Linux: invalid UTF-8 bytes in arguments are replaced with U+FFFD before use

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-016 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-016: Linux: invalid UTF-8 bytes in arguments are replaced with U+FFFD before use

- **Severity:** High
- **Area:** cli-options
- **Platform(s):** Linux
- **Confidence:** Confirmed. Detail: Confirmed. curl never re-encodes argv, so this does not depend on the REF version.
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-02`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *(WSL, echo.py on 20001):* `curl -s -d $'\xff' URL`; `-H $'X-A: \xff'`; `$'http://127.0.0.1:20001/\xff'`; `-o $'\xff.txt'`; `-o /dev/null -w $'\xfe'`; `--data-urlencode $'\xff'`; `-b $'a=\xff'`. Scripts: `linux/t3.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.

Commands (run each with REF and with PORT):

```sh
curl -s -d $'\xff' URL
```

**Expected (real curl)**

- the raw bytes are sent. Body 0xFF, header `X-A: \xff`, `GET /%FF`, file `\xff.txt`, `-w` writes 0xFE, `%FF`, `Cookie: a=\xff`.

**Actual (Curl)**

- every invalid byte becomes EF BF BD. Body EF BF BD (Content-Length 3), the header value EF BF BD, `GET /%EF%BF%BD`, file `EF BF BD.txt`, `-w` writes EF BF BD, `%EF%BF%BD`. The cookie value becomes `?`.

**Suspected location in source**

- argv reaches .NET already decoded as UTF-8 (Curl.Console Program entry) and is re-encoded per option, e.g. `CommandLineOptions.cs:2377`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr016Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-016 of the third-party gap report.
