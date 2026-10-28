---
id: BL-3710
title: Fix CGR-181: Proxy host messages: proxy 0 is not normalised to 0.0.0.0, and a proxy string starting with : reports the wrong reason
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/ProxyUrlParser.cs, Curl.Core.UnitTests/Cgr181Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3710 — Fix CGR-181: Proxy host messages: proxy 0 is not normalised to 0.0.0.0, and a proxy string starting with : reports the wrong reason

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-181 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-181: Proxy host messages: proxy `0` is not normalised to 0.0.0.0, and a proxy string starting with `:` reports the wrong reason

- **Severity:** Low
- **Area:** cli-options, environment
- **Platform(s):** Windows. Detail: [cli-options CLIO-18] Windows (REF-LINUX 8.18 words both messages differently, so Linux is not comparable)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-18`, `environment ENV-12`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- **[cli-options CLIO-18]** Proxy host messages: '0' is not normalised to 0.0.0.0, and ':' gives a different reason
  - `curl -sS --proxy 0 -o NUL http://127.0.0.1:20002/`; `curl -sS --proxy : -o NUL http://127.0.0.1:20002/`.
- **[environment ENV-12]** A proxy string that starts with `:` and has more colons (`::1`, `::::`) reports the wrong reason
  - `curl -sS -x ::1 http://127.0.0.1:21200/echo`
  - `set http_proxy=::::` then `curl -sS http://127.0.0.1:21200/echo`
  - Verifier cases: v-pxsyn and v-pxsyn-env.

Commands (run each with REF and with PORT):

```sh
# [cli-options CLIO-18]
curl -sS --proxy 0 -o NUL http://127.0.0.1:20002/
curl -sS --proxy : -o NUL http://127.0.0.1:20002/
# [environment ENV-12]
curl -sS -x ::1 http://127.0.0.1:21200/echo
set http_proxy=::::
curl -sS http://127.0.0.1:21200/echo
```

**Expected (real curl)**

- **[cli-options CLIO-18]** *REF 8.22:* `Failed to connect to 0.0.0.0:80 over proxy 0.0.0.0 ...` and `Unsupported proxy syntax in ':': No host present` (exit 5).
- **[environment ENV-12]** `curl: (5) Unsupported proxy syntax in '::1': No host present`

**Actual (Curl)**

- **[cli-options CLIO-18]** `Failed to connect to 0:80 over proxy 0 ...` and `... Port number was not a decimal number between 0 and 65535` (exit 5). The exit codes match.
- **[environment ENV-12]** `curl: (5) Unsupported proxy syntax in '::1': Port number was not a decimal number between 0 and 65535`. The exit code (5) is the same as REF.

**Suspected location in source**

- **[cli-options CLIO-18]** `Curl.Core.UnitLibrary/ProxyUrlParser.cs:119`.
- **[environment ENV-12]** not located.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr181Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-181 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

- 2026-10-28 results (real curl 8.22.0 Schannel): `--proxy 0` -> exit 7 `Failed to connect to 0.0.0.0:80 over proxy 0.0.0.0` (Curl already normalised it, BL-3575; pinned by Cgr181Tests). `--proxy :`, `-x ::1`, `http_proxy=::::` -> exit 5 `Unsupported proxy syntax in '<text>': No host present`. Fix: `ProxyUrlParser` finds an empty host before reading the port when the text has no scheme; with a scheme (`http://:abc`) the port reason stays, as measured earlier. Platform-neutral; Core fast tests green, quality gate clean for Curl.Core.UnitLibrary.
- 2026-10-28: Doing -> Done. Empty proxy host without a scheme now reports No host present; proxy 0 already normalised
