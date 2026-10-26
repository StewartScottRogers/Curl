---
id: BL-3638
title: Fix CGR-109: -Z without --parallel-immediate: the second same-host transfer does not wait for the first connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr109Tests.cs, Curl.Console/ParallelTransferMulti.cs, Curl.Console/ParallelTransferQueue.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3638 — Fix CGR-109: -Z without --parallel-immediate: the second same-host transfer does not wait for the first connection

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-109 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-109: -Z without --parallel-immediate: the second same-host transfer does not wait for the first connection

- **Severity:** Medium
- **Area:** network-robustness
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `network-robustness NR-7`

**Reproduction**

- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- `curl -sS -Z -m 2 -w '[%{num_connects}]' $H/stall $H/ok` (/stall accepts and never answers).

Commands (run each with REF and with PORT):

```sh
curl -sS -Z -m 2 -w '[%{num_connects}]' $H/stall $H/ok
```

**Expected (real curl)**

- the server sees 1 connection. Stdout `[1][0]`, two `(28) Operation timed out ...` lines, exit 28.

**Actual (Curl)**

- the server sees 2 connections. Stdout `[1]aaaaa[1]`, one `(28)` line, exit 28. With `--parallel-immediate` both open 2 connections and give the same output.

**Suspected location in source**

- the port's parallel scheduler has no equivalent of curl's default wait-for-multiplex (PIPEWAIT) when --parallel-immediate is not given.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr109Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-109 of the third-party gap report.
