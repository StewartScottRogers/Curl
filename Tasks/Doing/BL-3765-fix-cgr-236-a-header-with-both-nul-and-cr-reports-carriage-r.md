---
id: BL-3765
title: Fix CGR-236: A header with both NUL and CR reports Carriage return found in header instead of Nul byte in header
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpLine.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseHeadBuilder.cs, Curl.Protocol.Http.UnitTests/Cgr236Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3765 — Fix CGR-236: A header with both NUL and CR reports Carriage return found in header instead of Nul byte in header

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-236 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-236: A header with both NUL and CR reports `Carriage return found in header` instead of `Nul byte in header`

- **Severity:** Low
- **Area:** network-robustness
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `network-robustness NR-11`

**Reproduction**

- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- `curl -sS "$H/raw?b=$(b64 'HTTP/1.1 200 OK\r\nX: a\0b\rc\r\n\r\n')"` (also `X: a\rb\0c`).

Commands (run each with REF and with PORT):

```sh
curl -sS "$H/raw?b=$(b64 'HTTP/1.1 200 OK\r\nX: a\0b\rc\r\n\r\n')"
```

**Expected (real curl)**

- exit 8 `curl: (8) Nul byte in header`.

**Actual (Curl)**

- exit 8 `curl: (8) Carriage return found in header`.

**Suspected location in source**

- `HttpLine.cs:53` (the CR check runs before the NUL check in HttpResponseHeadBuilder.cs).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr236Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-236 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
