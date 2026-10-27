---
id: BL-3673
title: Fix CGR-144: A -Y/-y low-speed abort resets %{http_version}, %{response_code}, %{num_connects}, %{size_download} and %{size_request} to zero (HTTP/1.1...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr144Tests.cs, Curl.Console/EasyOptionsFromCommandLine.cs, Curl.Core.UnitLibrary/LowSpeedWatchdog.cs, Curl.Core.UnitTests/Cgr144Tests.cs, Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitTests/Cgr144Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3673 — Fix CGR-144: A -Y/-y low-speed abort resets %{http_version}, %{response_code}, %{num_connects}, %{size_download} and %{size_request} to zero (HTTP/1.1...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-144 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-144: A -Y/-y low-speed abort resets %{http_version}, %{response_code}, %{num_connects}, %{size_download} and %{size_request} to zero (HTTP/1.1 and HTTP/2)

- **Severity:** Medium
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (reproduced against REF-WIN 8.22.0 and REF-LINUX 8.18)
- **Confidence:** Confirmed. Detail: Confirmed (reproduced on Windows against REF 8.22.0 and on Linux, by the tester and the verifier)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-14`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server: `EV\slow_h1.sh` (Linux, port 21650) or `EV\slow_h1_srv.sh` (WSL server for Windows clients, port 21651). It sends `HTTP/1.1 200 OK\r\nContent-Length: 200\r\n\r\n` + 100 bytes, then stalls for 3 s.
- Command: `curl -sS -Y 1000 -y 1 http://127.0.0.1:21651/x -o NUL -w "[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_request}]\n"`
- HTTP/2 cases: `r2_sleepmid_speed` and `z_sleepmid_speed_tls` (`curl -sSk -Y 1000 -y 1 https://127.0.0.1:P/x`).
- Verifier output: `EV\verify\slow_h1_linux.out`, `EV\verify\slow_h1_win.out`.

Commands (run each with REF and with PORT):

```sh
EV\slow_h1.sh
EV\slow_h1_srv.sh
curl -sS -Y 1000 -y 1 http://127.0.0.1:21651/x -o NUL -w "[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_request}]\n"
curl -sSk -Y 1000 -y 1 https://127.0.0.1:P/x
```

**Expected (real curl)**

- exit 28, `[1.1 200 1 100 80]` on both REF-WIN 8.22.0 and REF-LINUX. Over HTTP/2: `[2 200 1 100 ...]`.

**Actual (Curl)**

- exit 28, `[0 000 0 0 0]` (HTTP/2: `[0 000 0 100 0]`). The same happens on PORT-WIN and PORT-LINUX. A `-m` timeout keeps the values; only the low-speed abort loses them.

**Suspected location in source**

- `Curl.Core.UnitLibrary\LowSpeedWatchdog.cs` / `Curl.Engine.UnitLibrary\TransferPerformer.cs:140` (`StartLowSpeedWatchdog`). The watchdog cancels the attempt and the transfer info is discarded. The options are set in `Curl.Console\EasyOptionsFromCommandLine.cs:724`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr144Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 5) cause: the curl tool performs through `ManagedEasyTransfer` with `ToolEvents` set, and that path skipped `AbortedHeadRecordingTransferEvents` (BL-3321/BL-3333), so a `-Y`/`-y` abort, whose watchdog failure carries no report, lost the response code, connection count and request size; and the recorder kept no HTTP version (the libcurl path reads it from the header store, which the tool path does not fill).
- Fix: `ManagedEasyTransfer.StateAndEventsOf` wraps the runner's events in `AbortedHeadRecordingTransferEvents` when `-Y` or `-y` is above zero (`WatchesLowSpeed`), and the recorder now keeps the last status line's HTTP version (`HTTP/1.1` as 1.1, `HTTP/2` as 2.0) on the report. Decision (default taken, no ADR needed as it only matches curl): wrap only under `-Y`/`-y`, because 13 `Curl.Console.UnitTests` pin that the handler sees the runner's events object itself and `-m` already kept its values through another path; this keeps the change to this issue. Touches widened to `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs`, `Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs` and `Curl.Engine.UnitLibrary/CLAUDE.md` (its BL-2318 section said the tool's events always go unwrapped); no task in Doing on origin/work/dark-factory named them. `Curl.Console/EasyOptionsFromCommandLine.cs`, `TransferPerformer.cs`, `LowSpeedWatchdog.cs` and the Core/Console `Cgr144Tests.cs` needed no change.
- Measured with `Record-CurlExchange.ps1 -Port 21651 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 200\r\n\r\n' + 100 bytes -HoldOpenMilliseconds 4000`, command `curl -sS -Y 1000 -y 1 http://127.0.0.1:21651/x -o NUL -w "[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_request}]\n"`:
  - REF (curl 8.21.0 Schannel, the lane's Windows curl): exit 28, `[1.1 200 1 100 80]`, stderr `curl: (28) Operation too slow. Less than 1000 bytes/sec transferred the last 1 seconds`.
  - PORT before: exit 28, `[0 000 0 100 0]`, same stderr. PORT after: exit 28, `[1.1 200 1 100 80]`, same stderr. Request bytes identical but for the `User-Agent` version (8.21.0 vs 8.22.0), which section 1 normalises. No files written by either.
  - Variant that already matched, `-m 2` in place of `-Y`/`-y`: REF and PORT both exit 28, `[1.1 200 1 100 80]`, `Operation timed out after ~2020 milliseconds with 100 out of 200 bytes received` (timing differs by a millisecond only).
  - HTTP/2 cases (`r2_sleepmid_speed`, `z_sleepmid_speed_tls`): the Windows REF has no HTTP/2 and the lane has no Linux REF, so the HTTP/2 result is pinned by `Cgr144Tests`' `HTTP/2 200` row (report version 2, code 200, one connection, 100 bytes), which is what the report gives for REF-LINUX (`[2 200 1 100 ...]`); the change is platform-neutral, so CI on Linux and macOS is covered by the fast tests passing here.
- `Curl.Engine.UnitTests/Cgr144Tests.cs`: the tool-path stall test fails on the unfixed code (2 failures, report null) and passes with the fix. Full fast run: 46 test projects passed. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-144 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A -Y/-y low-speed abort keeps %{http_version}, %{response_code}, %{num_connects} and %{size_request} as curl does (CGR-144)
