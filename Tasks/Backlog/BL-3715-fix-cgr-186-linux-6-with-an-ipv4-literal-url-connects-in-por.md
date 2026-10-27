---
id: BL-3715
title: Fix CGR-186: Linux: -6 with an IPv4-literal URL connects in PORT; REF 8.18 fails with exit 7
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/AddressFamilyFilter.cs, Curl.Networking.UnitTests/Cgr186Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3715 — Fix CGR-186: Linux: -6 with an IPv4-literal URL connects in PORT; REF 8.18 fails with exit 7

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-186 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-186: Linux: -6 with an IPv4-literal URL connects in PORT; REF 8.18 fails with exit 7

- **Severity:** Low
- **Area:** cli-options
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable. Windows REF 8.22 connects, the same as PORT, so this may depend on the version or platform.
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-24`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- `curl -s -6 http://127.0.0.1:20001/`.

Commands (run each with REF and with PORT):

```sh
curl -s -6 http://127.0.0.1:20001/
```

**Expected (real curl)**

- *REF 8.18:* exit 7.

**Actual (Curl)**

- exit 0.

**Suspected location in source**

- `Curl.Networking.UnitLibrary/AddressFamilyFilter.cs:26`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr186Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-186 of the third-party gap report.
