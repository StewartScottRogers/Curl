---
id: BL-3578
title: Fix CGR-049: A help line in a -K config file is silently ignored; curl prints the help page
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineParseResult.cs, Curl.Cli.UnitTests/Cgr049Tests.cs, Curl.Console, Curl.Console.UnitTests/Cgr049Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3578 — Fix CGR-049: A help line in a -K config file is silently ignored; curl prints the help page

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-049 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-049: A `help` line in a -K config file is silently ignored; curl prints the help page

- **Severity:** Medium
- **Area:** config-glob-output
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed on Windows and Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `config-glob-output CGO-05`

**Reproduction**

- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- with `k.cfg` containing `help\n`:
  - `curl -sS -K k.cfg $U/hp03`
  - `curl -sS -K k.cfg` (no URL)
  - Variants: `-h`, and `help = all` (`E\verify\v3_misc.ps1` ha).
  - Scripts: `E\verify\v2_misc.ps1` h1/h2 and `vl.sh` h1.

Commands (run each with REF and with PORT):

```sh
curl -sS -K k.cfg $U/hp03
curl -sS -K k.cfg
E\verify\v3_misc.ps1
E\verify\v2_misc.ps1
```

**Expected (real curl)**

- Writes the usage page to stdout (the full option list for `help = all`), then does the transfer with exit 0.
- With no URL, it prints the help, then `curl: (2) no URL specified`, exit 2.

**Actual (Curl)**

- Prints no help. It only does the transfer, or only prints `no URL specified`.
- The stdout bytes differ.

**Suspected location in source**

- `Curl.Cli.UnitLibrary/CommandLineOptions.cs:252` stores the subject in `ConfigFileHelpSubjects`, and `CommandLineParseResult.cs:116` exposes it. No code in Curl.Console reads it.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr049Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-049 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
