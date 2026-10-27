---
id: BL-3688
title: Fix CGR-159: With --doh-url, "localhost." (trailing dot) is sent to the DoH server instead of resolving to loopback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/Cgr159Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3688 — Fix CGR-159: With --doh-url, "localhost." (trailing dot) is sent to the DoH server instead of resolving to loopback

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-159 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-159: With --doh-url, "localhost." (trailing dot) is sent to the DoH server instead of resolving to loopback

- **Severity:** Medium
- **Area:** doh-ipfs-resolution
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0067
- **Source findings:** `doh-ipfs-resolution DOH-8`

**Reproduction**

- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- `srv doh=21800 web=21802` (no records), then `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://localhost.:21802/x -w "%{remote_ip} %{num_connects}\n"`. Without --doh-url both binaries behave the same (`v_f8_localhost_dot_nodoh`). Cases: `cases\v_f8_localhost_dot.txt`, `linux\cases\vl_f8_localhost_dot.txt`.

Commands (run each with REF and with PORT):

```sh
srv doh=21800 web=21802
curl -sS --doh-url http://127.0.0.1:21800/dns-query http://localhost.:21802/x -w "%{remote_ip} %{num_connects}\n"
```

**Expected (real curl)**

- exits 0 without sending any DoH query, connects to ::1 and prints `::1 1`.

**Actual (Curl)**

- sends A and AAAA queries for `localhost`, then exits 6 with `Could not resolve host: localhost.`.

**Suspected location in source**

- `Curl.Networking.UnitLibrary/TcpConnector.cs:1288-1290` (IsLocalhost does not accept a trailing dot), used at `DohDnsResolver.cs:126`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr159Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Fix: `TcpConnector.IsLocalhost` now drops one trailing dot before it matches, so `localhost.` and `<name>.localhost.` get `::1`, `127.0.0.1` without a DoH query. The system resolver, the HTTPS query and `--interface` share that check, so they now treat these names the same way. curl's own localhost check also accepts the trailing-dot forms. No ADR, because this only matches curl.
- Pipeline `feature` run as a direct change: the fix was one line, so no architect stage was needed (sensible default).
- Measured with `Record-CurlExchange.ps1 -Port 21802 -ListenAddress ::1` and no DoH responder, because real curl sends no query. REF = curl 8.22.0 Schannel (`C:\Program Files\Git\ucrt64\bin\curl.exe`), PORT = `dotnet run --project Curl.Console --no-build --`:
  - `curl -sS --doh-url http://127.0.0.1:21800/dns-query http://localhost.:21802/x -w "%{remote_ip} %{num_connects}\n"`: REF exit 0, stdout `::1 1`, stderr empty, request `GET /x HTTP/1.1`, `Host: localhost.:21802`, `User-Agent: curl/8.22.0`, `Accept: */*`. PORT identical: the stdout, stderr, exit code and request.bin hashes are equal. No files written by either. Before the fix PORT queried DoH and exited 6.
  - Variant without `--doh-url` (already matched): REF and PORT are still identical in all four files.
- Cgr159Tests: with the fix reverted, 5 of 8 cases failed; with it, all pass. dotnet build clean (0 warnings); fast tests all passed. Measure-CodeQuality -Library Curl.Networking.UnitLibrary: 0 failing members.
- Linux/macOS: the change and its tests are platform-neutral (no path, OS text or certificate) and the fast tests pass here. Linux REF was not measured; the report gives the same Expected for both platforms.

## Log

- 2026-10-26: Created from CGR-159 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. With --doh-url, localhost. resolves to loopback without a DoH query, as curl 8.22.0 does
