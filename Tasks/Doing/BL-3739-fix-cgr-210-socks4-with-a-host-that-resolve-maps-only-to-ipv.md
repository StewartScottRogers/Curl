---
id: BL-3739
title: Fix CGR-210: SOCKS4 with a host that --resolve maps only to IPv6: exit 97 "SOCKS4 connection to ::1 not supported"; curl 8.22.0 exits 6
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/Socks4Handshake.cs, Curl.Networking.UnitTests/Cgr210Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3739 — Fix CGR-210: SOCKS4 with a host that --resolve maps only to IPv6: exit 97 "SOCKS4 connection to ::1 not supported"; curl 8.22.0 exits 6

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-210 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-210: SOCKS4 with a host that --resolve maps only to IPv6: exit 97 "SOCKS4 connection to ::1 not supported"; curl 8.22.0 exits 6

- **Severity:** Low
- **Area:** proxy-connect
- **Platform(s):** Windows. Detail: Windows. The Linux REF is 8.18.0 and prints a third text, so Linux is version-dependent.
- **Confidence:** Confirmed. Detail: Confirmed (Windows).
- **Related existing finding (GF/AF):** none
- **Source findings:** `proxy-connect PC-09`

**Reproduction**

- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- run `srv 20600 log 'S4;S4OK;H;OK'`, then `curl -sS -x socks4://127.0.0.1:20600 --resolve ex.test:80:[::1] http://ex.test/x`. Verifier cases: v15 and VL13.

Commands (run each with REF and with PORT):

```sh
srv 20600 log 'S4;S4OK;H;OK'
curl -sS -x socks4://127.0.0.1:20600 --resolve ex.test:80:[::1] http://ex.test/x
```

**Expected (real curl)**

- *REF 8.22.0:* exit 6, `curl: (6) Could not resolve host: ex.test`. REF-LINUX 8.18.0: exit 97, `Could not resolve proxy: 127.0.0.1`.

**Actual (Curl)**

- exit 97, `curl: (97) SOCKS4 connection to ::1 not supported`.

**Suspected location in source**

- `Curl.Networking.UnitLibrary\Socks4Handshake.cs:136`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr210Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-210 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
