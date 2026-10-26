---
id: BL-3703
title: Fix CGR-174: Two concurrent -Z transfers writing the same -o file: PORT fails one of them with exit 23 on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs, Curl.Core.UnitTests/Cgr174Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3703 — Fix CGR-174: Two concurrent -Z transfers writing the same -o file: PORT fails one of them with exit 23 on Windows

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-174 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-174: Two concurrent -Z transfers writing the same -o file: PORT fails one of them with exit 23 on Windows

- **Severity:** Medium
- **Area:** parallel-multi-transfer
- **Platform(s):** Windows. Detail: Windows (Linux matches REF)
- **Confidence:** Confirmed. Detail: Confirmed (Windows x2. Linux: same as REF)
- **Related existing finding (GF/AF):** none
- **Source findings:** `parallel-multi-transfer PMT-6`

**Reproduction**

- *Setup (parallel-multi-transfer):* EV = `SCR\evidence\parallel-multi-transfer`. Server: `dotnet run EV\srv\server.cs -- 22000,22001 EV\srv\log` (listens on 127.0.0.1:22000 and :22001; query `d=` delay in ms, `s=` status, `n=` body size, `p=`/`pd=` send the body in parts with a delay, `close=1` adds Connection: close; other paths return `body /<path>`); port 22099 is closed. Linux: `EV/linux/srv.py 22000,22001 /tmp/pmt_srv` (started by `EV/linux/lt.sh`). Harness `EV\dt.ps1`; verifier `EV\v1.ps1` and `EV/linux/vl.sh`.
- `curl -Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{http_code}\n" -o same.txt "http://127.0.0.1:22000/a?n=20000&p=4&pd=200" -o same.txt "http://127.0.0.1:22000/b?n=20000&p=4&pd=200&d=100"`. Scripts: `v1.ps1` (v_same, v_same_r). Older runs: `b6.ps1`, `b7.ps1`.

Commands (run each with REF and with PORT):

```sh
curl -Z --parallel-immediate -sS -w "%{urlnum} %{exitcode} %{http_code}\n" -o same.txt "http://127.0.0.1:22000/a?n=20000&p=4&pd=200" -o same.txt "http://127.0.0.1:22000/b?n=20000&p=4&pd=200&d=100"
```

**Expected (real curl)**

- exit 0, stdout `0 0 200\n1 0 200\n` and no stderr. same.txt is 20000 bytes and holds the interleaved writes of both transfers.

**Actual (Curl)**

- exit 23, stdout `1 23 200\n0 0 200\n`, and stderr `curl: (23) client returned ERROR on write of 5000 bytes`. same.txt has different content.

**Suspected location in source**

- `Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs:141` and `:229`. Write opens use `FileShare.Read`, so a second writer's open fails on Windows. curl's `fopen` shares the file for writing.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr174Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-174 of the third-party gap report.
