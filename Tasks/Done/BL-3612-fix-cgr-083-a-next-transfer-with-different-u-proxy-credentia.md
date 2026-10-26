---
id: BL-3612
title: Fix CGR-083: A --next transfer with different -U proxy credentials reuses the earlier proxy connection; curl opens a new one
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ConnectionPoolProxyKey.cs, Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitTests/Cgr083Tests.cs, Curl.Networking.UnitLibrary/ConnectionPoolKey.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Proxy.cs, Curl.Console.UnitTests/CurlCompositionProxyTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3612 — Fix CGR-083: A --next transfer with different -U proxy credentials reuses the earlier proxy connection; curl opens a new one

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-083 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-083: A --next transfer with different -U proxy credentials reuses the earlier proxy connection; curl opens a new one

- **Severity:** Medium
- **Area:** proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0072
- **Source findings:** `proxy-connect PC-05`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- run `srv 20600 log 'H;OK;H;OK' 'H;OK'`, then:
  `curl -s -o NUL -w "%{num_connects}\n" -x http://127.0.0.1:20600 -U u:p http://a.test/1 --next -o NUL -w "%{num_connects}\n" -x http://127.0.0.1:20600 -U v:q http://a.test/2`
  On Linux use `-o /dev/null`. Verifier cases: v10 and VL08.

Commands (run each with REF and with PORT):

```sh
srv 20600 log 'H;OK;H;OK' 'H;OK'
curl -s -o NUL -w "%{num_connects}\n" -x http://127.0.0.1:20600 -U u:p http://a.test/1 --next -o NUL -w "%{num_connects}\n" -x http://127.0.0.1:20600 -U v:q http://a.test/2
```

**Expected (real curl)**

- opens two TCP connections. stdout is `1\n1\n`.

**Actual (Curl)**

- sends the second request (with `Basic djpx`) on the first connection, which was authenticated as u:p. stdout is `1\n0\n`.

**Suspected location in source**

- the connection-pool match does not compare plain-HTTP-proxy credentials (`Curl.Networking.UnitLibrary\ConnectionPoolProxyKey.cs` / `PoolingConnector.cs`; not narrowed further)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr083Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Cause (2026-10-26): a plain forward-proxy `ConnectTarget` (built in `HttpProtocolHandler.TargetOf`) carried no proxy, so `ConnectionPoolKey` keyed it without the credential that `ConnectionPoolProxyKey` already compares for tunnels. Fix: new `ConnectTarget.ForwardProxy` (the forward proxy endpoint with its credential), set by the HTTP handler; `ConnectionPoolKey.Of` keys on `target.Proxy ?? target.ForwardProxy`. This matches curl's `url.c` reuse check, which compares the proxy user and password for every proxy. No ADR: no design choice beyond matching curl.
- Touches widened (no other task in Doing on origin/work/dark-factory names these): `ConnectionPoolKey.cs` (the key), `ConnectTarget.cs` (carries the credential), `HttpProtocolHandler.cs` (sets it), `Networking/CLAUDE.md` (pool key doc), and two test files whose whole-record `ConnectTarget` expectations now compare `with { ForwardProxy = null }`, because `NetworkCredential` compares by reference (`HttpProtocolHandlerTests.Proxy.cs`, `CurlCompositionProxyTests.cs`). `ConnectionPoolProxyKey.cs` and `PoolingConnector.cs` needed no change. The TFTP and DoH forward-proxy targets are untouched; they send no proxy credential per request.
- Measured with a throwaway C# keep-alive loopback server (answers each request head with `200`, `Content-Length: 0`, keeps the connection; Record-CurlExchange.ps1 closes after each response, so it cannot show reuse). REF = curl 8.22.0 Schannel (mingw build), PORT = Curl.Console at this commit:
  - `curl -s -o NUL -w "%{num_connects}\n" -x http://127.0.0.1:20600 -U u:p http://a.test/1 --next -o NUL -w "%{num_connects}\n" -x http://127.0.0.1:20600 -U v:q http://a.test/2`: REF exit 0, stdout `1\n1\n`, stderr empty, two connections (conn 0 `Proxy-Authorization: Basic dTpw`, conn 1 `Basic djpx`). PORT (fixed): identical. PORT before the fix: `1\n0\n` (one connection), as the report gives it.
  - Variant that already matched, same `-U u:p` in both groups: REF and PORT exit 0, stdout `1\n0\n`, one connection, identical wire bytes.
- Focused test: `Curl.Networking.UnitTests/Cgr083Tests.cs`. Its different-credentials key test fails without the key change (both keys have a null proxy, so they are equal). Measure-CodeQuality over Networking, Http and Abstractions: 0 failing members. Fast tests green (the 11 failures from the first run were the record-equality expectations, fixed). Linux/macOS: the change is platform-neutral (no paths, OS text or certificates), so CI there is covered by keeping it so.

## Log

- 2026-10-26: Created from CGR-083 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A --next transfer with different -U proxy credentials opens a new forward-proxy connection, as curl 8.22.0 does (CGR-083)
