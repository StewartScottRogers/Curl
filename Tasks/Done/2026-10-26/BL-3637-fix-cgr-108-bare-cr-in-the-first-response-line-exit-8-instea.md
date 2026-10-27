---
id: BL-3637
title: Fix CGR-108: Bare CR in the first response line: exit 8 instead of 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpLine.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitTests/Cgr108Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3637 — Fix CGR-108: Bare CR in the first response line: exit 8 instead of 1

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-108 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-108: Bare CR in the first response line: exit 8 instead of 1

- **Severity:** Medium
- **Area:** network-robustness
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0012
- **Source findings:** `network-robustness NR-6`

**Reproduction**

- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- `curl -sS "$H/raw?b=$(b64 'abc\rdef\n')"`; `curl -sS "$H/raw?b=$(b64 'HTTP/1.1 2\r00 OK\r\n\r\n')"`. Random-byte replies (`/garbage?seed=N`) hit this often.

Commands (run each with REF and with PORT):

```sh
curl -sS "$H/raw?b=$(b64 'abc\rdef\n')"
curl -sS "$H/raw?b=$(b64 'HTTP/1.1 2\r00 OK\r\n\r\n')"
```

**Expected (real curl)**

- exit 1 `Received HTTP/0.9 when not allowed` / `Unsupported HTTP/1 subversion in response`.

**Actual (Curl)**

- exit 8 `curl: (8) Carriage return found in header`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpLine.cs:49-56` (the CR check runs before the status-line/HTTP-0.9 checks)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr108Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Added `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs` to `touches`: the status line is split there; no task in Doing on origin/work/dark-factory names it.
- Measured with `Record-CurlExchange.ps1 -Port 21300 -CurlArgs '-sS','http://127.0.0.1:21300/' -Response <r>`, the reference curl (Git for Windows) and then `-Curl Curl.Console/bin/Debug/net10.0/curl.exe`. Real curl parses the first line before it refuses a CR, so the CR check now runs after the status line parses (`HttpLine.SplitKeepingCarriageReturns` then `RefuseCarriageReturn`); header lines keep the old check. Real curl and Curl, after the fix, both gave (stdout empty, request bytes identical by `cmp`):
  - `abc\rdef\n` (issue repro 1): exit 1 `curl: (1) Received HTTP/0.9 when not allowed` (already matched before: the HTTP/0.9 check fires on the first bytes).
  - `HTTP/1.1 2\r00 OK\r\n\r\n` (issue repro 2): exit 1 `curl: (1) Unsupported HTTP/1 subversion in response` (Curl gave exit 8 before the fix).
  - `HTTP\r/1.1 200 OK\r\n\r\n`: exit 1 `Received HTTP/0.9 when not allowed`.
  - `HTTP/1.1 200 O\rK\r\n...`, `HTTP/1.0 200 OK\r\r\n...` and a header `A: b\rc`: exit 8 `curl: (8) Carriage return found in header`.
- `Cgr108Tests` pins all six; with the head reader's old `HttpLine.Split` the subversion case failed (1 of 6), with the fix all 6 pass.
- No ADR: the change only follows measured curl, with no new design choice.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members. Build clean, fast tests green on Windows; the change and test are platform-neutral (no paths, OS text or certificates).

## Log

- 2026-10-26: Created from CGR-108 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A bare CR in the first response line fails as curl does: HTTP/0.9 or unsupported subversion (exit 1) unless the line parses as a status line (exit 8)
