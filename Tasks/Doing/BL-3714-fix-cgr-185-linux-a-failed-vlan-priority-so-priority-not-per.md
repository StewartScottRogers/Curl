---
id: BL-3714
title: Fix CGR-185: Linux: a failed --vlan-priority (SO_PRIORITY not permitted) gives no warning
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3986]
touches: [Curl.Networking.UnitLibrary/QualityOfServiceSocketOptions.cs, Curl.Networking.UnitTests/Cgr185Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3714 — Fix CGR-185: Linux: a failed --vlan-priority (SO_PRIORITY not permitted) gives no warning

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-185 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-185: Linux: a failed --vlan-priority (SO_PRIORITY not permitted) gives no warning

- **Severity:** Low
- **Area:** cli-options
- **Platform(s):** Linux
- **Confidence:** Probable. Detail: Probable (REF-LINUX 8.18)
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-23`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *(non-root user):* `curl -o /dev/null --vlan-priority 7 http://127.0.0.1:20001/`.

Commands (run each with REF and with PORT):

```sh
curl -o /dev/null --vlan-priority 7 http://127.0.0.1:20001/
```

**Expected (real curl)**

- `Warning: VLAN priority 7 failed with errno 1: Operation not permitted`, exit 0.

**Actual (Curl)**

- no warning, exit 0.

**Suspected location in source**

- `Curl.Networking.UnitLibrary/QualityOfServiceSocketOptions.cs:88-90`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-10-28: Requeued. TcpDialer has no way to print a non-verbose `Warning:` line (ITransferEvents.ReportInfo is -v only); BL-3986 adds ITransferEvents.ReportWarning. Then: call events.ReportWarning("VLAN priority N failed with errno E: <strerror>") from TcpDialer when TrySet of the SO_PRIORITY option fails (QualityOfServiceSocketOptions.TrySet must return the SocketException; errno 1 = EPERM, text "Operation not permitted"), test in Cgr185Tests.cs. Not measurable on this Windows lane (SO_PRIORITY is Linux only).

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr185Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-185 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Needs BL-3986 (ITransferEvents.ReportWarning) so the dialer can print a non-verbose Warning: line
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
