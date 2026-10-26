---
id: BL-3699
title: Fix CGR-170: Under -Z, a later URL that cannot be set up (bad glob, unreadable -T file) makes REF abandon the whole batch, but PORT still runs the oth...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr170Tests.cs, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3699 — Fix CGR-170: Under -Z, a later URL that cannot be set up (bad glob, unreadable -T file) makes REF abandon the whole batch, but PORT still runs the oth...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-170 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-170: Under -Z, a later URL that cannot be set up (bad glob, unreadable -T file) makes REF abandon the whole batch, but PORT still runs the other transfers

- **Severity:** Medium
- **Area:** parallel-multi-transfer
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows x2, Linux)
- **Related existing finding (GF/AF):** none
- **Source findings:** `parallel-multi-transfer PMT-2`

**Reproduction**

- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- `curl -Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{url}\n" "http://127.0.0.1:22000/ok1?d=200" "http://127.0.0.1:22000/ok2?d=100" "http://[::1"`
- `curl -Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{http_code}\n" -T u1.txt "http://127.0.0.1:22000/a?d=200" -T nope.txt http://127.0.0.1:22000/b`. Here u1.txt exists and nope.txt does not.
- `curl -Z --parallel-max 1 -sS -w "%{urlnum} %{exitcode} %{url}\n" "http://127.0.0.1:22000/ok1?d=200" "http://127.0.0.1:22000/ok2?d=100" "http://[::1"`

Scripts: `v1.ps1` (v_badglob, v_badglob_r, v_upmiss, v_badglob_pmax1, v_badglob_ser), `linux/vl.sh` (vl_badglob, vl_upmiss).

Commands (run each with REF and with PORT):

```sh
curl -Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{url}\n" "http://127.0.0.1:22000/ok1?d=200" "http://127.0.0.1:22000/ok2?d=100" "http://[::1"
curl -Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{http_code}\n" -T u1.txt "http://127.0.0.1:22000/a?d=200" -T nope.txt http://127.0.0.1:22000/b
curl -Z --parallel-max 1 -sS -w "%{urlnum} %{exitcode} %{url}\n" "http://127.0.0.1:22000/ok1?d=200" "http://127.0.0.1:22000/ok2?d=100" "http://[::1"
```

**Expected (real curl)**

- no request goes out for ok1, ok2 or a. Each transfer already queued is reported with the setup error. stdout is `0 3 …ok1?d=200\n1 3 …ok2?d=100\n`. stderr has the glob error and then two `curl: (3) URL using bad/illegal format or missing URL` lines. In the -T case, stdout is `0 26 000\n1 26 000\n` and `curl: (26) Failed to open/read local data from file/application` appears twice. Exit is 3 or 26. With `--parallel-max 1`, ok1 and ok2 run, and then REF prints `2 3 http://[::1` and `curl: (3) URL rejected: Bad IPv6 address`. Serial mode is the same in REF and PORT.

**Actual (Curl)**

- prints the glob or cannot-open error once, then performs the other transfers and reports them. stdout is `body /ok2\n1 0 …\nbody /ok1\n0 0 …\n`, and in the -T case u1.txt is uploaded (`1 26 000\nbody /a\n0 0 200\n`). The exit code is the same as REF's. With `--parallel-max 1`, PORT prints no -w line and no `URL rejected` line for the bad URL.

**Suspected location in source**

- `Curl.Console/CurlCommandRunner.cs` ~1993-2002, the -Z path that records "a failure that ends the run before its transfer starts", and `WriteGlobFailureLinesAsync` at :1713. Neither aborts nor re-reports transfers that are already queued.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr170Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-170 of the third-party gap report.
