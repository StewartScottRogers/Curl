---
id: BL-3530
title: Fix CGR-001: HTTP/1.1 response desync: after a Content-Length body followed by extra bytes, PORT keeps the connection and returns the stale bytes as t...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, Curl.Protocol.Http.UnitTests/Cgr001Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpLineReader.cs, Curl.Protocol.Http.UnitTests/HttpLineReaderTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3530 — Fix CGR-001: HTTP/1.1 response desync: after a Content-Length body followed by extra bytes, PORT keeps the connection and returns the stale bytes as t...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-001 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-001: HTTP/1.1 response desync: after a Content-Length body followed by extra bytes, PORT keeps the connection and returns the stale bytes as the response to the next URL

- **Severity:** Critical
- **Area:** http-responses, network-robustness
- **Platform(s):** Both (Windows and Linux). Detail: [http-responses HR-1] Both (Windows and Linux); [network-robustness NR-8] Windows (not run on Linux).
- **Confidence:** Confirmed. Detail: [http-responses HR-1] Confirmed. The original tester reproduced it 3 times on Windows and once on Linux; the verifier reproduced it again on both.; [http-responses HR-2] Confirmed (Windows and Linux)
- **Related existing finding (GF/AF):** GF-0072, GF-0002
- **Source findings:** `http-responses HR-1`, `http-responses HR-2`, `network-robustness NR-8`

Same root cause in all three findings: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1096` (`SettleConnection`) marks the connection reusable without checking for bytes read past the Content-Length body. curl prints `* Excess found ...` and closes the connection.

**Reproduction**

- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- **[http-responses HR-1]** Response desync: after a Content-Length response followed by extra bytes, PORT reuses the connection and returns the stale bytes as the reply to the next URL
  - `EV\resp\desync_16384.bin` = `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhe` + 16344 x `P` + `HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nEVIL`, with opts `keep`.
  - `EV\resp\desync_b.bin` = `HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\nGOOD`, with opts `keep`.
  - Script: `EV\repro_desync.ps1`.
  - Command: `curl -v http://127.0.0.1:20400/desync_16384 http://127.0.0.1:20400/desync_b`
  - Variant: `desync_65536` (64K of padding), run with `-s -S`.
- **[http-responses HR-2]** Connection reused after excess body bytes where curl closes it (%{num_connects}, missing "Excess found")
  - `keep_two_bigcl.bin` = `HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhello`, opts `keep`.
    Command: `curl -s -S -w "%{num_connects}\n" http://127.0.0.1:20400/keep_two_bigcl http://127.0.0.1:20400/keep_two_bigcl`
  - `keep_204_body.bin` = `HTTP/1.1 204 No\r\nContent-Length: 5\r\n\r\nhelloHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok`, opts `keep`.
    Command: `curl -s -S -w "%{num_connects} %{http_code}\n" <url> <url>`
- **[network-robustness NR-8]** Excess bytes after a Content-Length body do not stop connection reuse
  - `curl -s -w '[%{num_connects}]' "$H/raw?keep=1&b=$(b64 'HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nabcdef')" $H/ok`.

Commands (run each with REF and with PORT):

```sh
# [http-responses HR-1]
EV\repro_desync.ps1
curl -v http://127.0.0.1:20400/desync_16384 http://127.0.0.1:20400/desync_b
# [http-responses HR-2]
curl -s -S -w "%{num_connects}\n" http://127.0.0.1:20400/keep_two_bigcl http://127.0.0.1:20400/keep_two_bigcl
curl -s -S -w "%{num_connects} %{http_code}\n" <url> <url>
# [network-robustness NR-8]
curl -s -w '[%{num_connects}]' "$H/raw?keep=1&b=$(b64 'HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nabcdef')" $H/ok
```

**Expected (real curl)**

- **[http-responses HR-1]** prints `* Excess found writing body: excess = 16386, size = 2, maxdownload = 2, bytecount = 2`, then `* shutting down connection #0`, and opens a new connection for URL 2. stdout is `heGOOD`, exit 0. REF-LINUX 8.18 does the same.
- **[http-responses HR-2]** `he1\nhe1\n`, and for the 204 case `1 204\n1 204\n`. The second URL uses a new connection.
- **[network-robustness NR-8]** closes the connection and opens a new one. The server sees 2 connections; stdout `ab[1]aaaaa[1]`.

**Actual (Curl)**

- **[http-responses HR-1]** prints `Connection #0 ... left intact`, then `Reusing existing http: connection`.
  - 16K padding: stdout is `heEVIL`, exit 0. The injected bytes are taken as the response to URL 2.
  - 64K padding: stdout is `he`, exit 8 `curl: (8) Invalid status line`, because the leftover `PPPP...` bytes are parsed as URL 2's status line.
  - Same result on Linux.
- **[http-responses HR-2]** `he1\nhe0\n`, and for the 204 case `1 204\n0 204\n`. The connection is reused. Same on Linux.
- **[network-robustness NR-8]** reuses the connection. The server sees 1 connection; stdout `ab[1]aaaaa[0]`.

**Suspected location in source**

- **[http-responses HR-1]** `Curl.Protocol.Http.UnitLibrary\HttpProtocolHandler.cs:1096` (`SettleConnection`) marks the connection reusable when the outcome is "left intact". Nothing checks for bytes already read past the Content-Length body. `HttpResponseBodyReader.cs` reports leftovers only for chunked bodies.
- **[http-responses HR-2]** same root cause as HR-1, `HttpProtocolHandler.cs:1096` `SettleConnection`. This is the observable symptom when the stale bytes happen to be harmless.
- **[network-robustness NR-8]** `Curl.Protocol.Http.UnitLibrary` has no "Excess found" handling (grep finds nothing); the persistence decision is in HttpProtocolHandler

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr001Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Fix: `HttpResponseBodyReader` reports bytes read past a Content-Length body, or after the head of a 204/304 (not for -I), as one data event and curl's `Excess found writing body: excess = N, size = S, maxdownload = S, bytecount = S` line, and sets `FoundExcess`; `HttpProtocolHandler.KeepsAlive` then does not keep an HTTP/1.x connection, so -v says `shutting down connection #0` and the next URL opens a new connection.
- `HttpLineReader` first read raised from 16384 to 102400 (MaximumLineLength): curl 8.22.0 counts excess = 16386 for HR-1, so its read held the whole rest; with 16 KiB Curl counted 16344. The doubling branch became unreachable (a line filling the buffer is refused first) and was removed. Added HttpLineReader.cs and HttpLineReaderTests.cs to touches for this; no task in Doing on origin/work/dark-factory named them. Design choice taken under the delegation, recorded here (small, follows measured curl; no ADR needed).
- Measured 2026-10-26 with Record-CurlExchange.ps1 (-HoldOpenMilliseconds 2000 -AnswerHeldRequests 1 -Connections 2), REF = curl 8.22.0 mingw Schannel, PORT = Curl.Console after the fix; request bytes identical apart from the port:
  - HR-1 `-v URL1 URL2` (16K padding): REF and PORT exit 0, stdout `heGOOD`, `{ [16388 bytes data]`, `* Excess found writing body: excess = 16386, size = 2, maxdownload = 2, bytecount = 2`, `* shutting down connection #0`, `* Connection #1 ... left intact`. Before the fix PORT printed no Excess line and reused the connection.
  - HR-1 64K padding `-s -S -v`: both exit 0, stdout `heGOOD`, excess = 65538.
  - HR-2 keep_two_bigcl `-s -S -w "%{num_connects}\n"`: both `he1\nhe1\n`. 204 case `-w "%{num_connects} %{http_code}\n"`: both `1 204\n1 204\n`, `{ [45 bytes data]` and excess = 45, size = 0 per URL.
  - NR-8 `-s -w "[%{num_connects}]"` (CL 2 + abcdef, then a 5-byte reply): both `ab[1]aaaaa[1]`.
- Focused tests in Cgr001Tests.cs: the two excess tests failed with the excess report disabled and pass with it. Fast tests green. Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: first run found CopyAsync complexity 12 (fixed by extracting ReportBytesAfterBodilessHead) and MakeRoom's uncovered branches (compaction now covered by a new HttpLineReader test; dead doubling branch removed); the final change was not re-measured for budget. Platform check: tests use loopback-free scripted connections and no paths, so they are platform-neutral; CI on Linux/macOS is left to the shift's CI watch.

## Log

- 2026-10-26: Created from CGR-001 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Bytes past a Content-Length body (or after a 204/304 head) print curl's Excess found line and close the connection instead of becoming the next response
