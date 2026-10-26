---
id: BL-3585
title: Fix CGR-056: The IPv6 zone id is dropped from the absolute-form request line sent to an HTTP proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitTests/Cgr056Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitTests/Cgr056Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3585 — Fix CGR-056: The IPv6 zone id is dropped from the absolute-form request line sent to an HTTP proxy

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-056 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-056: The IPv6 zone id is dropped from the absolute-form request line sent to an HTTP proxy

- **Severity:** Medium
- **Area:** url-parsing, proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-10`, `proxy-connect PC-08`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- **[url-parsing UP-10]** The IPv6 zone id is dropped from the absolute-form request line sent to an HTTP proxy
  - `curl -sS -x http://127.0.0.1:20200 "http://[::1%25lo]:8080/a"`, with the test server acting as the proxy (v10_proxy_zone; Linux lv10_proxy_zone)
- **[proxy-connect PC-08]** An IPv6 zone id is dropped from the absolute-form URL of a plain proxy request; curl keeps "%25zone"
  - run `srv 20600 log 'H;OK'`, then `curl -s -x http://127.0.0.1:20600 "http://[fe80::1%25eth0]:8080/x"`. Verifier cases: v14 and VL12.

Commands (run each with REF and with PORT):

```sh
# [url-parsing UP-10]
curl -sS -x http://127.0.0.1:20200 "http://[::1%25lo]:8080/a"
# [proxy-connect PC-08]
srv 20600 log 'H;OK'
curl -s -x http://127.0.0.1:20600 "http://[fe80::1%25eth0]:8080/x"
```

**Expected (real curl)**

- **[url-parsing UP-10]** `GET http://[::1%25lo]:8080/a HTTP/1.1`
- **[proxy-connect PC-08]** `GET http://[fe80::1%25eth0]:8080/x HTTP/1.1`.

**Actual (Curl)**

- **[url-parsing UP-10]** `GET http://[::1]:8080/a HTTP/1.1`
- **[proxy-connect PC-08]** `GET http://[fe80::1]:8080/x HTTP/1.1`.

**Suspected location in source**

- **[url-parsing UP-10]** `CurlUrlHost.cs:209-213` (`normalized` is built without the zone id) and the proxy request-target formatter.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr056Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Cause: `CurlUrlHost` keeps the zone id apart from `Host` rightly (curl's `Host:` line has no zone), but `HttpRequestHeadFormatter.AbsoluteForm` built the proxy target from the zoneless host. Fix: `ZonedHostAndPort` puts `%25<zone>` before the `]` in the absolute form only. So the fix and its test are in Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs and Curl.Protocol.Http.UnitTests/Cgr056Tests.cs. Both were added to `touches`, and no task in Doing on origin/work/dark-factory names them. The Abstractions files were left unchanged.
- Measured with Record-CurlExchange.ps1 (server replies `200 OK`, body `ok`), real curl 8.22.0 Schannel and Curl:
  - `curl -sS -x http://127.0.0.1:P "http://[fe80::1%25eth0]:8080/x"`: both send `GET http://[fe80::1%25eth0]:8080/x HTTP/1.1`, `Host: [fe80::1]:8080`, `User-Agent: curl/8.22.0`, `Accept: */*`, `Proxy-Connection: Keep-Alive`; both exit 0, print stdout `ok` and write empty stderr. request.bin is byte-identical.
  - `curl -sS -x http://127.0.0.1:P "http://[::1%25lo]:8080/a"`: both send `GET http://[::1%25lo]:8080/a HTTP/1.1` with `Host: [::1]:8080`; both exit 0 and print `ok`. request.bin is byte-identical.
- Cgr056Tests failed 3 of 5 before the fix (the zoned proxy targets) and pass after it. The zoneless and origin-form cases (variants that already matched) still pass. Measure-CodeQuality -Library Curl.Protocol.Http.UnitLibrary: 0 failing members. The change is platform-neutral (string formatting only), so CI on Linux and macOS should agree.

## Log

- 2026-10-26: Created from CGR-056 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A forward proxy's absolute-form target keeps the IPv6 zone id as %25zone, byte-identical to curl 8.22.0
