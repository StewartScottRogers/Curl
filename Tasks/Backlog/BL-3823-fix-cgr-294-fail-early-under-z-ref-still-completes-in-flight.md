---
id: BL-3823
title: Fix CGR-294: --fail-early under -Z: REF still completes in-flight transfers whose response arrives before its abort lands (about 1 s), but PORT cancel...
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3822]
touches: [Curl.Console.UnitTests/Cgr294Tests.cs, Curl.Console.UnitTests/ParallelRunTests.cs, Curl.Console.UnitTests/CurlCommandRunnerParallelTests.cs, Curl.Console/ParallelRun.cs, Curl.Console/CurlCommandRunner.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3823 — Fix CGR-294: --fail-early under -Z: REF still completes in-flight transfers whose response arrives before its abort lands (about 1 s), but PORT cancel...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-294 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-294: --fail-early under -Z: REF still completes in-flight transfers whose response arrives before its abort lands (about 1 s), but PORT cancels them at once

- **Severity:** Low (timing-dependent)
- **Area:** parallel-multi-transfer
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed. It was the same on every run (Windows x2, Linux x1). It depends on timing, because REF only stops at its next check.
- **Related existing finding (GF/AF):** none
- **Source findings:** `parallel-multi-transfer PMT-9`

**Reproduction**

- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- `curl -Z --parallel-immediate -s --fail-early -f -w "%{urlnum} %{exitcode} %{http_code}\n" "http://127.0.0.1:22000/a?d=1500" "http://127.0.0.1:22000/b?d=200&s=500" "http://127.0.0.1:22000/c?d=900"`. Scripts: `v1.ps1` (v_fe1, v_fe1_r), `linux/vl.sh` (vl_fe1).

Commands (run each with REF and with PORT):

```sh
curl -Z --parallel-immediate -s --fail-early -f -w "%{urlnum} %{exitcode} %{http_code}\n" "http://127.0.0.1:22000/a?d=1500" "http://127.0.0.1:22000/b?d=200&s=500" "http://127.0.0.1:22000/c?d=900"
```

**Expected (real curl)**

- stdout is `1 22 500\nbody /c\n0 42 000\n2 42 200\n`, so c's body is written. The run takes about 950 ms. Exit 22.

**Actual (Curl)**

- stdout is `1 22 500\n0 42 000\n2 42 000\n`. c's body is not written and its http_code is 000. The run takes about 250 ms. Exit 22.

**Suspected location in source**

- `Curl.Console/ParallelRun.cs`. `RecordEnd` with failEarly cancels every transfer immediately.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr294Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-28 (lane 2): Work done but uncommitted (stashed): ParallelRun.RecordEnd now sets HasFailedEarly/StopStartsToken at once and cancels the abort token after AbortDelay (1 s, curl checks about once a second); CurlCommandRunner uses HasFailedEarly to stop starts, run.StopStartsToken for host waits, and reports a wait cancelled by the failure as aborted; Cgr294Tests added, ParallelRunTests and CurlCommandRunnerParallelTests updated (an in-flight transfer ending before the abort now completes). Console fast tests pass (3086). Not yet done: real-curl repro comparison, Measure-CodeQuality for Curl.Console. Needs CurlCommandRunner.cs, held by BL-3822 in Doing, so requeued with depends-on BL-3822.

## Log

- 2026-10-26: Created from CGR-294 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Needs Curl.Console/CurlCommandRunner.cs held by BL-3822; depends-on BL-3822
