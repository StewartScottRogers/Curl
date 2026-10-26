---
id: BL-3706
title: Fix CGR-177: Port-only --ai-help and --httpsig-* options are accepted and listed, and curl --help prints an extra --ai-help line
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Cli.UnitLibrary/CurlHelpTable.cs, Curl.Cli.UnitTests/Cgr177Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3706 — Fix CGR-177: Port-only --ai-help and --httpsig-* options are accepted and listed, and curl --help prints an extra --ai-help line

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-177 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-177: Port-only `--ai-help` and `--httpsig-*` options are accepted and listed, and `curl --help` prints an extra `--ai-help` line

- **Severity:** Low
- **Area:** cli-options, config-glob-output, environment
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [config-glob-output CGO-12] Confirmed on Windows and Linux.; [environment ENV-16] Confirmed. On Windows this is the only difference. On Linux the rest of the help text differs because of the 8.18 version.
- **Related existing finding (GF/AF):** GF-0040
- **Source findings:** `cli-options CLIO-14`, `config-glob-output CGO-12`, `environment ENV-16`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- **[cli-options CLIO-14]** PORT-only options --ai-help and --httpsig-* are accepted and appear in the help output
  - `curl --ai-help`; `curl -s --httpsig-key x @U`; compare `curl -h` and `curl --help all`. Script: `repro/r09_extra_opts_win.sh`.
- **[config-glob-output CGO-12]** `curl --help` has an extra line only the port prints
  - `curl --help`. Scripts: `E\verify\v3_misc.ps1` hp and `vl.sh` hp.
- **[environment ENV-16]** `curl --help` has an extra line advertising `--ai-help`
  - `curl --help` (v-help on both platforms)

Commands (run each with REF and with PORT):

```sh
# [cli-options CLIO-14]
curl --ai-help
curl -s --httpsig-key x @U
curl -h
curl --help all
# [config-glob-output CGO-12]
curl --help
E\verify\v3_misc.ps1
# [environment ENV-16]
```

**Expected (real curl)**

- **[cli-options CLIO-14]** exit 2 `curl: option --ai-help: is unknown` (the same for `--httpsig-key`).
- **[config-glob-output CGO-12]** the page ends with `Use "--help [option]" to view documentation for a given option`.
- **[environment ENV-16]** No such line.

**Actual (Curl)**

- **[cli-options CLIO-14]** `--ai-help` prints Markdown help (exit 0). `--httpsig-key x` gives exit 2 with a different message (`--httpsig-keyid is required`, which `-s` hides). `-h` and `--help all/http/auth/curl` have extra lines.
- **[config-glob-output CGO-12]** adds a final line, `Use "--ai-help" for Markdown help written for AI agents (Curl only)`.
- **[environment ENV-16]** Line 24 is the extra line `Use "--ai-help" for Markdown help written for AI agents (Curl only)`. The original tester also reported that `--help all` lists the port-only `--ai-help` and `--httpsig-*` rows.

**Suspected location in source**

- **[cli-options CLIO-14]** `Curl.Cli.UnitLibrary/CurlHelpTable.cs`, `CurlAiHelpText.cs`.
- **[config-glob-output CGO-12]** the Curl.Cli.UnitLibrary help text (the `--ai-help` addition).
- **[environment ENV-16]** not located.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr177Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-177 of the third-party gap report.
