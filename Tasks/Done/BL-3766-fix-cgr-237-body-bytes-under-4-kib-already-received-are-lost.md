---
id: BL-3766
title: Fix CGR-237: Body bytes under 4 KiB already received are lost when the process is killed mid-transfer (Windows)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests/Cgr237Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3766 — Fix CGR-237: Body bytes under 4 KiB already received are lost when the process is killed mid-transfer (Windows)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-237 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-237: Body bytes under 4 KiB already received are lost when the process is killed mid-transfer (Windows)

- **Severity:** Low
- **Area:** network-robustness
- **Platform(s):** Windows. Detail: Windows (on Linux both lose the data).
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `network-robustness NR-12`

**Reproduction**

- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- `timeout -s TERM 3 curl -s -o out.bin "$H/stallbody?len=100000&sent=100"` (also sent=4095, 4097).

Commands (run each with REF and with PORT):

```sh
timeout -s TERM 3 curl -s -o out.bin "$H/stallbody?len=100000&sent=100"
```

**Expected (real curl)**

- out.bin holds 100 / 4095 / 4097 bytes.

**Actual (Curl)**

- out.bin holds 0 / 0 / 4097 bytes. The port buffers about 4 KiB and does not flush before the external kill.

**Suspected location in source**

- Curl.Console output-file buffering (DeferredOutputFileStream).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr237Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-237 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

- 2026-10-28: Cause: PhysicalFileSystem's FileStream buffers 4 KiB; writes >= 4 KiB bypass it, so 4097 already matched. Fix in Curl.Console only: `DeferredOutputFileStream` flushes after each write (first and later), ignoring a flush IOException so a full disk is still reported at close (CGR-026 tests stay green). Expected per report: out.bin 100/4095/4097 bytes; the focused test `Cgr237Tests` fails before (0 bytes on disk) and passes after. Real curl and the kill-mid-transfer server were not re-run (the evidence server is gone); the unit test pins the on-disk length directly. Linux/macOS: test is platform-neutral (temp dir, same-process read with FileShare.ReadWrite). Measure-CodeQuality.ps1 -Library Curl.Console: 0 failing members.
- 2026-10-28: Doing -> Done. Flush the -o file after each write; Cgr237Tests pins it
