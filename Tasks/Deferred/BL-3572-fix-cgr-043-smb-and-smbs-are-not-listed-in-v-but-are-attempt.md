---
id: BL-3572
title: Fix CGR-043: smb:// and smbs:// are not listed in -V but are attempted; --proto =smb and --proto-default smb are accepted
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs, Curl.Cli.UnitTests/Cgr043Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3572 — Fix CGR-043: smb:// and smbs:// are not listed in -V but are attempted; --proto =smb and --proto-default smb are accepted

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-043 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-043: smb:// and smbs:// are not listed in -V but are attempted; --proto =smb and --proto-default smb are accepted

- **Severity:** Medium
- **Area:** cli-options
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0037, GF-0091
- **Source findings:** `cli-options CLIO-08`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- `curl -sS smb://127.0.0.1:20002/share/x`; `curl --proto =smb -sS smb://127.0.0.1:20002/share/x`; `curl --proto-default smb -sS 127.0.0.1:20002/share/x`.

Commands (run each with REF and with PORT):

```sh
curl -sS smb://127.0.0.1:20002/share/x
curl --proto =smb -sS smb://127.0.0.1:20002/share/x
curl --proto-default smb -sS 127.0.0.1:20002/share/x
```

**Expected (real curl)**

- `curl: (1) Protocol "smb" is disabled` (exit 1). `--proto =smb` gives `curl: unrecognized protocol 'smb'` and `option --proto: is badly used here` (exit 2). `--proto-default smb` gives `a specified protocol is unsupported by libcurl` (exit 1).

**Actual (Curl)**

- all three try to connect and give exit 7 `Failed to connect ...`.

**Suspected location in source**

- `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs` / the SMB protocol registration.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr043Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-043 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Dark factory stopped at its 80-turn cap, so the task is too big for one run: split it in the first turns of the next claim; see Z:\repos\Curl.logs\BL-3572-20261026-063535-L2.jsonl
- 2026-10-26: Backlog -> Deferred. Over its cost cap; split into BL-3887, BL-3888, BL-3889
