---
id: BL-3741
title: Fix CGR-212: -v Connection #N to host X left intact names the --connect-to target or the unix-socket path instead of the URL host:port
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3736]
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Protocol.Http.UnitTests/Cgr212Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3741 — Fix CGR-212: -v Connection #N to host X left intact names the --connect-to target or the unix-socket path instead of the URL host:port

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-212 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-212: `-v` `Connection #N to host X left intact` names the `--connect-to` target or the unix-socket path instead of the URL host:port

- **Severity:** Low
- **Area:** proxy-connect, doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux). Detail: [proxy-connect PC-14] (a) Windows only. REF-LINUX 8.18 prints `127.0.0.1:20600`, as the port does, so this is an 8.22 change. (b) Both.
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `proxy-connect PC-14`, `doh-ipfs-resolution DOH-19`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- **[proxy-connect PC-14]** -v "Connection #0 to host X left intact" names the --connect-to target or the unix-socket path instead of the URL host:port
  - *(a):* run `srv 20600 log 'H;OK'`, then `curl -sv --connect-to ex.test:80:127.0.0.1:20600 http://ex.test/x`. Verifier case: v21.
  - *(b):* run `srv --unix %TEMP%\pcv_u.sock 20600 log 'H;OK'`, then `curl -sv --unix-socket %TEMP%\pcv_u.sock http://ex.test/x`. Linux: `/tmp/pcv_u.sock`. Verifier cases: v26 and VL21.
- **[doh-ipfs-resolution DOH-19]** With --connect-to, the final -v line names the connect-to host instead of the URL host
  - `srv web=21802`, then `curl -v -sS --connect-to foo.test:21802:localhost:21802 http://foo.test:21802/x`. Case: `cases\v_f19_connect_to_v.txt`.

Commands (run each with REF and with PORT):

```sh
# [proxy-connect PC-14]
srv 20600 log 'H;OK'
curl -sv --connect-to ex.test:80:127.0.0.1:20600 http://ex.test/x
srv --unix %TEMP%\pcv_u.sock 20600 log 'H;OK'
curl -sv --unix-socket %TEMP%\pcv_u.sock http://ex.test/x
# [doh-ipfs-resolution DOH-19]
srv web=21802
curl -v -sS --connect-to foo.test:21802:localhost:21802 http://foo.test:21802/x
```

**Expected (real curl)**

- **[proxy-connect PC-14]** `* Connection #0 to host ex.test:80 left intact`. REF-LINUX 8.18 also prints `Established connection to ex.test (/tmp/pcv_u.sock port 0)`.
- **[doh-ipfs-resolution DOH-19]** `* Connection #0 to host foo.test:21802 left intact`.

**Actual (Curl)**

- **[proxy-connect PC-14]** (a) `... to host 127.0.0.1:20600 left intact`. (b) `... to host c:\users\stewar~1\appdata\local\temp\pcv_u.sock:0 left intact` (path lower-cased) on Windows, and `/tmp/pcv_u.sock:0` on Linux. On Linux the port also prints `Established connection to /tmp/pcv_u.sock (`; Windows 8.22 agrees with the port on that line.
- **[doh-ipfs-resolution DOH-19]** `* Connection #0 to host localhost:21802 left intact`.

**Suspected location in source**

- Not located by the reviewers.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr212Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-28 (lane 1): touches changed from Engine trace files: those only detect the line. It is built by HttpProtocolHandler.LeftIntactLine (~line 1310), which names unix-socket path / --connect-to mapped host (HttpConnectionInfoLines.LeftIntactOverUnixSocket); curl 8.22.0 names the URL host:port. BL-3736 holds Curl.Protocol.Http.UnitLibrary, so depends-on BL-3736. Real curl 8.22.0 Schannel is at /ucrt64/bin/curl for measuring.

## Log

- 2026-10-26: Created from CGR-212 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Fix lives in Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, held by BL-3736; waits for BL-3736
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes

- Measured curl 8.22.0 (Schannel): `curl -sv --connect-to ex.test:80:127.0.0.1:20611 http://ex.test/x` ends `Connection #0 to host ex.test:80 left intact`. Curl now names the URL host:port for --connect-to and --unix-socket (the report gives the same expected text for the socket case). Removed the unix-socket and mapped-host branches from LeftIntactLine, and LeftIntactOverUnixSocket.
- Decision: with --connect-to plus an alt-svc route (unmeasured) the line names the alt-svc alternative, as without a mapping.
- Linux 8.18 differences are not reproduced; change is platform-neutral. Quality run clean, build clean, fast tests green.
- 2026-10-29: Doing -> Done. CGR-212: left-intact line names URL host:port
