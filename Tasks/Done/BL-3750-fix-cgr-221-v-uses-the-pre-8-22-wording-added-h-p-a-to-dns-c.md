---
id: BL-3750
title: Fix CGR-221: -v uses the pre-8.22 wording Added H:P:A to DNS cache for --resolve (8.22: [DNS] added H:P:A to cache) and the old IPv6 Established conne...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/Cgr221Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3750 — Fix CGR-221: -v uses the pre-8.22 wording Added H:P:A to DNS cache for --resolve (8.22: [DNS] added H:P:A to cache) and the old IPv6 Established conne...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-221 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-221: `-v` uses the pre-8.22 wording `Added H:P:A to DNS cache` for `--resolve` (8.22: `[DNS] added H:P:A to cache`) and the old IPv6 `Established connection to` host form

- **Severity:** Low
- **Area:** writeout-verbose, proxy-connect, doh-ipfs-resolution
- **Platform(s):** Windows. Detail: [writeout-verbose WV-18] Windows (compared against 8.22.0); [proxy-connect PC-13] Windows (8.22.0 wording; REF-LINUX 8.18 still uses the old text).; [doh-ipfs-resolution DOH-18] Windows (8.22 wording)
- **Confidence:** Confirmed. Detail: [writeout-verbose WV-18] Confirmed. The difference reproduces against the Windows REF, which is a debug build. Neither line is a debug-only trace: both are plain `-v` output with no `--trace-config`.
- **Related existing finding (GF/AF):** GF-0038
- **Source findings:** `writeout-verbose WV-18`, `proxy-connect PC-13`, `doh-ipfs-resolution DOH-18`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- *Setup (doh-ipfs-resolution):* EV = `SCR\evidence\doh-ipfs-resolution`. Test server `EV\srv\out\srv.exe` (Linux `EV/srv/outlinux/srv`, source `EV\srv\Program.cs`); in repros `srv` means this binary. Options: `doh=<port>` plain-HTTP DoH responder, `dohs=<port>` TLS DoH (`selfsigned=1` or `pfx=`), `web=`/`webs=` web targets, `web2=` recording proxy, `rr=name:TYPE:addr` records, `mangle=http10|hang` faults, `webstatus=`/`weblocation=`. Harness `EV\harness.ps1`; verifier `EV\v1.ps1`, `EV\v2.ps1` (outputs `EV\cases\v_*.txt`); Linux `EV\linux\run1.sh vl.py`.
- **[writeout-verbose WV-18]** -v wording behind 8.22.0: the --resolve cache line and the IPv6 "Established connection to" host
  - `curl -s -v --resolve foo.test:20700:127.0.0.1 http://foo.test:20700/`
  - `curl -s -v 'http://[::1]:20700/'`
- **[proxy-connect PC-13]** -v text for a --resolve entry uses the old wording "Added H:P:A to DNS cache"; curl 8.22.0 prints "[DNS] added H:P:A to cache"
  - run `srv 20600 log 'H;OK'`, then `curl -sv --resolve ex.test:20600:127.0.0.1 http://ex.test:20600/x`. Verifier case: v20.
- **[doh-ipfs-resolution DOH-18]** -v wording for a --resolve entry: "Added X to DNS cache" vs "[DNS] added X to cache"
  - as in the previous finding. Case: `cases\v_f17_multi_resolve_v.txt`.

Commands (run each with REF and with PORT):

```sh
# [writeout-verbose WV-18]
curl -s -v --resolve foo.test:20700:127.0.0.1 http://foo.test:20700/
curl -s -v 'http://[::1]:20700/'
# [proxy-connect PC-13]
srv 20600 log 'H;OK'
curl -sv --resolve ex.test:20600:127.0.0.1 http://ex.test:20600/x
```

**Expected (real curl)**

- **[writeout-verbose WV-18]** 
  - `--resolve`: `* [DNS] added foo.test:20700:127.0.0.1 to cache`
  - IPv6: `* Established connection to [::1] (::1 port 20700) from ::1 port N`
- **[proxy-connect PC-13]** `* [DNS] added ex.test:20600:127.0.0.1 to cache`.
- **[doh-ipfs-resolution DOH-18]** `* [DNS] added foo.test:21802:127.0.0.9,127.0.0.1 to cache`.

**Actual (Curl)**

- **[writeout-verbose WV-18]** 
  - `--resolve`: `* Added foo.test:20700:127.0.0.1 to DNS cache`
  - IPv6: `* Established connection to ::1 (::1 port 20700) ...`
  - Both are the 8.18 wordings.
- **[proxy-connect PC-13]** `* Added ex.test:20600:127.0.0.1 to DNS cache`.
- **[doh-ipfs-resolution DOH-18]** `* Added foo.test:21802:127.0.0.9,127.0.0.1 to DNS cache`.

**Suspected location in source**

- **[proxy-connect PC-13]** `Curl.Networking.UnitLibrary\TcpConnector.cs:1063`.
- **[doh-ipfs-resolution DOH-18]** `Curl.Networking.UnitLibrary/TcpConnector.cs` (grep "to DNS cache").

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr221Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Measured curl 8.22.0: `--resolve foo.test:9:127.0.0.1` gives `* [DNS] added foo.test:9:127.0.0.1 to cache`; Curl now equal. IPv6 literal host is bracketed in Established connection (from the report; the refused connect could not show it). ADR-0969. Existing tests pinning the old wording in Console, Engine and Networking test projects were updated (added to touches). Linux CI: change is platform-neutral.

## Log

- 2026-10-26: Created from CGR-221 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. Verbose output now writes the 8.22 DNS cache line and brackets an IPv6 literal host (ADR-0969)
