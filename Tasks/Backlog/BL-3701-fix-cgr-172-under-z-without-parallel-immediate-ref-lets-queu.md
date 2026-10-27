---
id: BL-3701
title: Fix CGR-172: Under -Z without --parallel-immediate, REF lets queued same-host transfers start when any transfer ends; PORT holds them until that host'...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr172Tests.cs, Curl.Console/ParallelHostQueue.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3701 — Fix CGR-172: Under -Z without --parallel-immediate, REF lets queued same-host transfers start when any transfer ends; PORT holds them until that host'...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-172 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-172: Under -Z without --parallel-immediate, REF lets queued same-host transfers start when any transfer ends; PORT holds them until that host's first transfer ends

- **Severity:** Medium
- **Area:** parallel-multi-transfer
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows x2 for each case, Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `parallel-multi-transfer PMT-4`

**Reproduction**

- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- `curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" "http://127.0.0.1:22001/q?d=100" "http://127.0.0.1:22000/c?d=300"`
- `curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" http://127.0.0.1:22099/x "http://127.0.0.1:22000/c?d=200"`. Port 22099 refuses the connection.

Scripts: `v1.ps1` (v_q1_other, v_q1_other_r, v_refmid, v_refmid_r), `linux/vl.sh` (vl_q1_other, vl_refmid).

Commands (run each with REF and with PORT):

```sh
curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" "http://127.0.0.1:22001/q?d=100" "http://127.0.0.1:22000/c?d=300"
curl -Z -s -w "%{urlnum} %{exitcode} %{http_code}\n" -o NUL -o NUL -o NUL "http://127.0.0.1:22000/a?d=600" http://127.0.0.1:22099/x "http://127.0.0.1:22000/c?d=200"
```

**Expected (real curl)**

- c starts on a second connection as soon as q ends (t=116 ms), or right after the refused connect, and finishes before a. stdout is `1 0 200\n2 0 200\n0 0 200\n`; in the refused case it is `1 7 000\n2 0 200\n0 0 200\n`.

**Actual (Curl)**

- c waits until a ends at about 605 ms and then reuses a's connection. stdout is `1 0 200\n0 0 200\n2 0 200\n`; in the refused case it is `1 7 000\n0 0 200\n2 0 200\n`.

**Suspected location in source**

- `Curl.Console/ParallelHostQueue.cs` ~80-95. `Leave` frees only the waiters of the same host:port, so a transfer that ends at another host, or that fails to connect, frees none of them.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr172Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-172 of the third-party gap report.
