---
id: BL-3599
title: Fix CGR-070: An unsolicited 101 Switching Protocols is treated as an interim 1xx response, so the body is not written and the transfer fails with exit 8
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitTests/Cgr070Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Protocol.Http.UnitTests/HttpResponseHeadReaderTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3599 — Fix CGR-070: An unsolicited 101 Switching Protocols is treated as an interim 1xx response, so the body is not written and the transfer fails with exit 8

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-070 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-070: An unsolicited `101 Switching Protocols` is treated as an interim 1xx response, so the body is not written and the transfer fails with exit 8

- **Severity:** Medium
- **Area:** http-responses, network-robustness
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0083
- **Source findings:** `http-responses HR-3`, `network-robustness NR-4`

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- **[http-responses HR-3]** Unsolicited 101 Switching Protocols is treated as an interim 1xx, so the body fails with exit 8
  - `x101b.bin` = `HTTP/1.1 101 Switching\r\n\r\nhello`, then the server closes. Command: `curl -s -S http://127.0.0.1:20400/x101b`
  - Variants: `x101` (`Upgrade: foo`, run with `-i`), `x101c` (`Content-Length: 2`), `x101_10` (`HTTP/1.0 101`).
- **[network-robustness NR-4]** Unsolicited `101 Switching Protocols` fails with exit 8 `Invalid status line`
  - `curl -sSi "$H/raw?b=$(b64 'HTTP/1.1 101 Switching\r\n\r\nabc')"`; add `-w '%{http_code} %{exitcode}'`. Also with `Upgrade: h2c`/`Connection: Upgrade` headers (Linux l_101b).

Commands (run each with REF and with PORT):

```sh
# [http-responses HR-3]
curl -s -S http://127.0.0.1:20400/x101b
# [network-robustness NR-4]
curl -sSi "$H/raw?b=$(b64 'HTTP/1.1 101 Switching\r\n\r\nabc')"
```

**Expected (real curl)**

- **[http-responses HR-3]** exit 0. stdout is `hello`. With `-i`, the 101 head is followed by `hello`.
- **[network-robustness NR-4]** exit 0, stdout `HTTP/1.1 101 Switching\r\n\r\nabc`; -w `101 0`.

**Actual (Curl)**

- **[http-responses HR-3]** exit 8 `curl: (8) Invalid status line`, and no body is written. With `-i` only the 101 head is written. Same on Linux.
- **[network-robustness NR-4]** exit 8 `curl: (8) Invalid status line`; the body is not written; -w `101 8`.

**Suspected location in source**

- **[http-responses HR-3]** `Curl.Protocol.Http.UnitLibrary\HttpResponseHeadReader.cs` around line 470-517: 101 is treated as an interim response (`SwitchedProtocols |= StatusCode == 101` at line 517).
- **[network-robustness NR-4]** `HttpResponseHeadReader.cs` treats 101 as informational and expects another status line

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr070Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 5): touches widened to `HttpProtocolHandler.cs` (sets `AsksH2cUpgrade`, no "Assume close" line for a 101), `HttpResponseBodyReader.cs` (a final 101's body runs to close with no framing headers, empty one exits 52) and `HttpResponseHeadReaderTests.cs` (the h2c interim test now sets `AsksH2cUpgrade`; the "101 with no upgrade asked for" exit-52 row moved to `Cgr070Tests` at handler level). No task in Doing on origin/work/dark-factory named any of them.
- Measured with `Record-CurlExchange.ps1` (port 20499, URL `http://127.0.0.1:20499/x`). REF is curl 8.22.0 (Git for Windows ucrt64 build). PORT is `Curl.Console/bin/Debug/net10.0/curl.exe` after the fix. Every row gives the same output from both, and request.bin is byte-identical for every row:
  - `-s -S`, `HTTP/1.1 101 Switching\r\n\r\nhello` (HR-3 x101b): exit 0, stdout `hello`, stderr empty.
  - `-s -S -i`, `...101 Switching\r\nUpgrade: foo\r\n\r\nhello` (x101): exit 0, stdout is the head, then `hello`.
  - `-s -S`, `...101 Switching\r\nContent-Length: 2\r\n\r\nhello` (x101c): exit 0, stdout `hello` (Content-Length ignored).
  - `-s -S`, `HTTP/1.0 101 Switching\r\n\r\nhello` (x101_10): exit 0, stdout `hello`.
  - `-sSi -w '%{http_code} %{exitcode}'`, `HTTP/1.1 101 Switching\r\n\r\nabc` (NR-4): exit 0, stdout `HTTP/1.1 101 Switching\r\n\r\nabc101 0`.
  - `-sSi`, the same with `Upgrade: h2c` and `Connection: Upgrade` (l_101b): exit 0, stdout is the head, then `abc`.
  - `-sSi`, a 101 followed by an `HTTP/1.1 200` head: exit 0, and the 200 head is written as the 101's body.
  - Before the fix, PORT exited 8 with `Invalid status line` on each of these (as the report says; the new `Cgr070Tests` fail the same way without the fix).
- Measured more for REF: a 101 with an empty body (`...101 Switching\r\nContent-Length: 0\r\n\r\n` or `A: 1`, then close) exits 52, `Empty reply from server`, after writing the head with `-i`. With `-v`, curl writes no `no chunk, no close, no size` line, and ends with `left intact`. Curl now matches all of this except `left intact`: it still shuts the connection down after a 101 (ADR-0050), so that `-v` line is filed as BL-3867.
- Decision recorded in ADR-0824. A 101 is interim only when the request asked for an h2c upgrade (`HttpH2cUpgradeConnection`) or the heads come from an HTTP/2 or HTTP/3 stream.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members. Fast tests are green on Windows. The tests need no file system, path or OS error text, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-070 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. An unsolicited 101 is the final head: its body is written to the close and the transfer exits 0 (52 when empty), as curl 8.22.0 does
