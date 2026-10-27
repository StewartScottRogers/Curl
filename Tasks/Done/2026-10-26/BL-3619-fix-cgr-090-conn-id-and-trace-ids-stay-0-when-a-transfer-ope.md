---
id: BL-3619
title: Fix CGR-090: %{conn_id} and --trace-ids stay 0 when a transfer opens a new connection (redirect hop, or a retry after a dead reused connection)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr090Tests.cs, Curl.Console/ConnectionIdRecordingTransferEvents.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3619 — Fix CGR-090: %{conn_id} and --trace-ids stay 0 when a transfer opens a new connection (redirect hop, or a retry after a dead reused connection)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-090 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-090: %{conn_id} and --trace-ids stay 0 when a transfer opens a new connection (redirect hop, or a retry after a dead reused connection)

- **Severity:** Medium
- **Area:** writeout-verbose
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `writeout-verbose WV-09`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- `curl -s -o NUL -w '%{conn_id}\n' -L http://127.0.0.1:20700/redir/2`
- `curl -s -v --trace-ids -L http://127.0.0.1:20700/redir/1`
- `curl -sS -w '%{conn_id}\n' http://127.0.0.1:20700/ka http://127.0.0.1:20700/close`

Commands (run each with REF and with PORT):

```sh
curl -s -o NUL -w '%{conn_id}\n' -L http://127.0.0.1:20700/redir/2
curl -s -v --trace-ids -L http://127.0.0.1:20700/redir/1
curl -sS -w '%{conn_id}\n' http://127.0.0.1:20700/ka http://127.0.0.1:20700/close
```

**Expected (real curl)**

- `redir/2`: `2`
- `--trace-ids`: second-hop lines are prefixed `[0-1]`.
- Dead-connection retry: second conn_id is `1`.

**Actual (Curl)**

- `redir/2`: `0`
- `--trace-ids`: `[0-0]`, while the same output still says `* shutting down connection #1`.
- Dead-connection retry: `0`.

**Suspected location in source**

- `Curl.Console/ConnectionIdRecordingTransferEvents.cs:28-36`. `ReportConnectionOpened` records the id in the dictionary but never updates `state.ConnectionId`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr090Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 6): measured with `Record-CurlExchange.ps1` against curl 8.22.0 Schannel (`/ucrt64/bin/curl`, a debug build) and Curl at eefa0496 built to a scratch folder. The recorder closes each connection after its response, so the repros were rebuilt as: (1) `-s -o NUL -w '%{conn_id}\n' -L http://127.0.0.1:20790/a` with three responses `302 Location: /b`, `302 Location: /c` (both `Connection: close`) and `200 ok`; (2) `-s -v --trace-ids -L http://127.0.0.1:20791/a` with `302 Location: /b` then `200 ok`; (3) `-sS -w '%{conn_id}\n' http://127.0.0.1:20793/ka http://127.0.0.1:20793/close` with two keep-alive `200 ok` responses, the second URL's reuse finding the connection dead and connecting afresh (also run with `-v --trace-ids`).
- Real curl: (1) stdout `2`, exit 0; (2) every line through `* Issue another request to this URL: 'http://127.0.0.1:20791/b'` is `[0-0]`, from `* Hostname 127.0.0.1 was found in DNS cache` on `[0-1]`, ending `[0-1] * shutting down connection #1`; (3) stdout `ok0` / `ok1`, exit 0, the second transfer `[1-0]` through its `Issue another request` line and `[1-1]` after it. Curl before the fix: (1) `0`; (2) every line `[0-0]`, even beside `shutting down connection #1`; (3) `ok0` / `ok0`, all `[1-0]`.
- Fix: `ConnectionIdRecordingTransferEvents.ReportInfo` clears the transfer's `%{conn_id}` once it has passed on a line starting with `RedirectFollower.IssueAnotherRequestMessagePrefix`, so the next line or opened connection takes the next number, exactly where curl moves to it. The plan was small enough to do directly rather than through the full `/feature` stages; no ADR, as it only matches measured curl. Pinned by `Curl.Console.UnitTests/Cgr090Tests.cs` (the first test gets `0` before the fix).
- After the fix all three match: stdout and request bytes byte for byte, exit codes equal, stderr equal once the debug build's own lines (`!!! WARNING !!!`, `sending last upload chunk`, `Curl_xfer_send`, `sendrecv_dl`, `Curl_sendrecv`) and the local port are dropped. One line left aside as not this issue: on the dead connection real curl said `Recv failure: Connection was reset` and Curl `Recv failure: Connection was aborted`, which depends on how the recorder's close reached each one. Variants that already matched (the first transfer's lines, the first hop's id) still match.
- Build clean; fast tests green (46 test projects, 0 failures, Curl.Console.UnitTests 2931 passed). `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members. The change is platform-neutral (no paths, OS errors or certificates), so CI on Linux and macOS is expected to stay green.

## Log

- 2026-10-26: Created from CGR-090 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. %{conn_id} and --trace-ids take the next connection number after 'Issue another request to this URL', on a redirect hop or a fresh connect after a dead reused connection, as curl 8.22.0 does
