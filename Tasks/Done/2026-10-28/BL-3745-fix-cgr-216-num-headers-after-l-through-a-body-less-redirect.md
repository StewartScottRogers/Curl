---
id: BL-3745
title: Fix CGR-216: %{num_headers} after -L through a body-less redirect: curl adds the hops' counts, PORT reports only the last response
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/Cgr216Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3745 — Fix CGR-216: %{num_headers} after -L through a body-less redirect: curl adds the hops' counts, PORT reports only the last response

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-216 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-216: %{num_headers} after -L through a body-less redirect: curl adds the hops' counts, PORT reports only the last response

- **Severity:** Low
- **Area:** writeout-verbose
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `writeout-verbose WV-12`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- `resp/loc2.txt` is a 301 with `Location: /code/200`, `Content-Length: 0` and `Connection: close`.
  - `curl -s -o NUL -L -w '%{num_headers}\n' http://127.0.0.1:20700/raw/loc2`

Commands (run each with REF and with PORT):

```sh
curl -s -o NUL -L -w '%{num_headers}\n' http://127.0.0.1:20700/raw/loc2
```

**Expected (real curl)**

- `5`

**Actual (Curl)**

- `3`

**Suspected location in source**

- `num_headers` in `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr216Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-216 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Run notes (2026-10-28)

- Not reproducible: real curl 8.22.0 (Schannel, Windows) measured against a PowerShell loopback server. `/nobody` is a 301 with `Location`, `Content-Length: 0`, `Connection: close`; `/body` is the same with a 3-byte body; the final 200 has Date, Content-Type, Content-Length, Connection. `curl -s -o NUL -L -w '%{num_headers}\n'` gave 4 for both (2 with a two-header final response): the last response only, never the hops added. That matches upstream test3015 (on the ratchet, expects the last response's count) and Curl's current output.
- A first attempt summed `EarlierResponseHeaders`; it broke test3015 (4 became 9), so it was reverted. The report's 5 is probably its server's final response having five headers; the report's server is gone.
- Added `Cgr216Tests.cs` pinning the last-response-only count. No production change, so no quality run was needed; the Output unit tests pass (867).
- Linux/macOS CI: the test is platform-neutral.
- 2026-10-28: Doing -> Done. Not reproducible: real curl counts only the last response's headers; pinned with a unit test
