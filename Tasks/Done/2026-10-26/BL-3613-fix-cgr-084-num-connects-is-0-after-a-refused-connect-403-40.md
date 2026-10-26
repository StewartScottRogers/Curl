---
id: BL-3613
title: Fix CGR-084: %{num_connects} is 0 after a refused CONNECT (403/407) or a failed SOCKS handshake; curl reports 1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitTests/Cgr084Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3613 — Fix CGR-084: %{num_connects} is 0 after a refused CONNECT (403/407) or a failed SOCKS handshake; curl reports 1

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-084 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-084: %{num_connects} is 0 after a refused CONNECT (403/407) or a failed SOCKS handshake; curl reports 1

- **Severity:** Medium
- **Area:** proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `proxy-connect PC-06`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- *(CONNECT):* run `srv 20600 log 'HN;407B'`, then `curl -s -o NUL -w "%{http_connect} %{num_connects}\n" -p -x http://127.0.0.1:20600 http://example.test/`.
- *(SOCKS5):* run with script `S5G;S5NONE`, then `curl -s -o NUL -w "%{num_connects} %{http_code} %{http_connect}\n" -x socks5://127.0.0.1:20600 http://127.0.0.1:8080/x`.

Commands (run each with REF and with PORT):

```sh
srv 20600 log 'HN;407B'
curl -s -o NUL -w "%{http_connect} %{num_connects}\n" -p -x http://127.0.0.1:20600 http://example.test/
curl -s -o NUL -w "%{num_connects} %{http_code} %{http_connect}\n" -x socks5://127.0.0.1:20600 http://127.0.0.1:8080/x
```

**Expected (real curl)**

- prints `407 1` and `1 000 000`.

**Actual (Curl)**

- prints `407 0` and `0 000 000`.

**Notes**

- *Verifier cases:* v11, v12, VL09 and VL10.

**Suspected location in source**

- `Curl.Engine.UnitLibrary\ManagedEasyTransfer.Finish.cs:208` (`NumConnectsOf`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr084Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured 2026-10-26 with `Record-CurlExchange.ps1`, REF = Git for Windows ucrt64 curl 8.22.0 (Schannel), PORT = `Curl.Console\bin\Debug\net10.0\curl.exe`:
  - CONNECT 407 (`-Response 'HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm="x"\r\nContent-Length: 0\r\n\r\n'`), `curl -s -o NUL -w "%{http_connect} %{num_connects}\n" -p -x http://127.0.0.1:20613 http://example.test/`: REF exit 7, stdout `407 1`, stderr empty; PORT before the fix `407 0`, after `407 1`, exit 7, stderr empty; both sent the same `CONNECT example.test:80 HTTP/1.1` request bytes.
  - CONNECT 403 variant: REF and PORT after the fix both exit 7, `403 1`.
  - SOCKS5 (`-Script` reading the greeting `05 02 00 01` and answering `05 ff`), `curl -s -o NUL -w "%{num_connects} %{http_code} %{http_connect}\n" -x socks5://127.0.0.1:20614 http://127.0.0.1:8080/x`: REF exit 97, `1 000 000`; PORT before `0 000 000`, after `1 000 000`, exit 97, stderr empty, same bytes on the wire.
  - Variant that already matched: proxy refused at TCP level (`-x http://127.0.0.1:20616`, nothing listening): REF and PORT both exit 7, `0 000`; still matches.
- Fix: `ManagedEasyTransfer.NumConnectsOf` counts 1 when the report counted none and the proxy answered the tunnel (`HttpConnectCode > 0`) or a SOCKS handshake failed with a `CURLPX_*` code (`ProxyError > 0`); the failed-connection checks moved into `CountsAFailedConnection` to keep its cyclomatic complexity under 10. No ADR: no design choice beyond matching curl.
- Focused tests: `Curl.Engine.UnitTests/Cgr084Tests.cs` (403, 407, a failed SOCKS handshake, and a refused connect staying 0); the first three return 0 before the fix.
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members. Full fast run green; the change is platform-neutral (no path, OS error or certificate), which covers CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-084 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. %{num_connects} is 1 after a refused CONNECT (403/407) or a failed SOCKS handshake, as curl 8.22.0
