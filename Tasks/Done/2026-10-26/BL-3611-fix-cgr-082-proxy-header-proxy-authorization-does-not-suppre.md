---
id: BL-3611
title: Fix CGR-082: --proxy-header "Proxy-Authorization: ..." does not suppress the -U Proxy-Authorization on a plain proxy request (two headers sent)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HttpProxyTunnel.cs, Curl.Networking.UnitTests/Cgr082Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitTests/Cgr082Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3611 — Fix CGR-082: --proxy-header "Proxy-Authorization: ..." does not suppress the -U Proxy-Authorization on a plain proxy request (two headers sent)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-082 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-082: --proxy-header "Proxy-Authorization: ..." does not suppress the -U Proxy-Authorization on a plain proxy request (two headers sent)

- **Severity:** Medium
- **Area:** proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed. The CONNECT path matches REF, according to the tester.
- **Related existing finding (GF/AF):** none
- **Source findings:** `proxy-connect PC-04`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- run `srv 20600 log 'H;OK'`, then `curl -s -x http://127.0.0.1:20600 -U u:p --proxy-header 'Proxy-Authorization: custom' http://example.test/`. Verifier cases: v09 and VL07.

Commands (run each with REF and with PORT):

```sh
srv 20600 log 'H;OK'
curl -s -x http://127.0.0.1:20600 -U u:p --proxy-header 'Proxy-Authorization: custom' http://example.test/
```

**Expected (real curl)**

- sends only `Proxy-Authorization: custom`.

**Actual (Curl)**

- sends `Proxy-Authorization: Basic dTpw` after Host and also `Proxy-Authorization: custom` at the end.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\HttpRequestHeadFormatter.cs` (Proxy-Authorization is always appended; compare `HttpProxyTunnel.cs:83` `AppendUnlessNamed`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr082Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-082 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Measured with Record-CurlExchange.ps1 against real curl 8.22.0 (Schannel, Windows), each command also run through Curl's built curl.exe; exit code, stdout, stderr and request bytes compared with cmp:
  - `curl -s -x http://127.0.0.1:P -U u:p --proxy-header 'Proxy-Authorization: custom' http://example.test/` -> exit 0, request `GET http://example.test/ HTTP/1.1`, `Host`, `User-Agent`, `Accept`, `Proxy-Connection: Keep-Alive`, `Proxy-Authorization: custom` (no Basic). Curl before the fix also sent `Proxy-Authorization: Basic dTpw` after Host; after it, all four outputs are byte-identical.
  - Variant `-H 'Proxy-Authorization: custom'` instead: real curl sends both (Basic after Host, custom at the end); Curl already matched and still does.
  - Variant `-H 'X-A: 1' --proxy-header 'Proxy-Authorization: custom' -H 'Proxy-Authorization: h'`: real curl sends no Basic, then `X-A`, `h`, `custom`; Curl matches.
- Fix: `HttpRequestHeadFormatter.Format` leaves out the authenticator's `Proxy-Authorization` when a `--proxy-header` value names it (as libcurl's `Curl_checkProxyheaders` under `CURLHEADER_SEPARATE`), and still sends it beside an `-H` one. The CONNECT path (`HttpProxyTunnel.cs`) already did this, so it is unchanged. No ADR: the behaviour is real curl's, measured, with no design choice left open.
- Focused tests in `Curl.Protocol.Http.UnitTests/Cgr082Tests.cs`; `Format_ProxyHeaderNamesProxyAuthorization_SendsOnlyIt` fails on the old `AppendAlways` line (it wrote the Basic line after Host unconditionally) and passes now.
- `dotnet build` clean; fast tests green (46 test assemblies, 0 failed; Curl.Protocol.Http.UnitTests 2377 passed). Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members. Change is platform-neutral (header text only).
- 2026-10-26: Doing -> Done. A --proxy-header Proxy-Authorization replaces the -U one on a plain proxy request, as curl 8.22.0 does
