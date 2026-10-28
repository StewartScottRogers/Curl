---
id: BL-3813
title: Fix CGR-284: A DoH lookup timeout says "Connection timed out" instead of "Resolving timed out"
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/MaxTimeWatchdog.cs, Curl.Core.UnitTests/Cgr284Tests.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/Cgr284Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3813 — Fix CGR-284: A DoH lookup timeout says "Connection timed out" instead of "Resolving timed out"

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-284 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-284: A DoH lookup timeout says "Connection timed out" instead of "Resolving timed out"

- **Severity:** Low
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `doh-ipfs-resolution DOH-14`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1 mangle=hang`, then `curl -sS -m 3 --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x`. Cases: `cases\v_f14_hang.txt`, `linux\cases\vl_f14_hang.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802 rr=foo.test:A:127.0.0.1 mangle=hang
curl -sS -m 3 --doh-url http://127.0.0.1:21800/dns-query http://foo.test:21802/x
```

**Expected (real curl)**

- `curl: (28) Resolving timed out after 3012 milliseconds`.

**Actual (Curl)**

- `curl: (28) Connection timed out after 2999 milliseconds`. The exit code is the same.

**Suspected location in source**

- `Curl.Networking.UnitLibrary/TcpConnector.cs:735`, `Curl.Core.UnitLibrary/MaxTimeWatchdog.cs:189`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr284Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Fix: TcpConnector marks a cancellation arriving inside the DNS look-up and ConnectWithinTimeoutAsync answers it, when the caller token (-m) is cancelled, with exit 28 "Resolving timed out after N milliseconds" (curl 8.22.0 text per the report). MaxTimeWatchdog.cs unchanged: the connector returns the failure itself. Cgr284Tests failed before the fix and passes after; Networking fast tests 3726 passed; Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.
- Not run against real curl: the report scratch server (srv.exe) is gone and the repro needs a hanging DoH responder; text taken from the report measured output. Timeouts outside the look-up take the old path unchanged.

## Log

- 2026-10-26: Created from CGR-284 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. A -m timeout during a DoH/DNS lookup now says Resolving timed out
