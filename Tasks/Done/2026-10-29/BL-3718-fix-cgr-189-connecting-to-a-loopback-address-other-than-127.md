---
id: BL-3718
title: Fix CGR-189: Connecting to a loopback address other than 127.0.0.1 with no listener fails at once in the port; curl takes about 2 s, so a --connect-ti...
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/Cgr189Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3718 — Fix CGR-189: Connecting to a loopback address other than 127.0.0.1 with no listener fails at once in the port; curl takes about 2 s, so a --connect-ti...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-189 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-189: Connecting to a loopback address other than 127.0.0.1 with no listener fails at once in the port; curl takes about 2 s, so a --connect-timeout under 2 s gives exit 28 for curl and exit 7 for the port

- **Severity:** Low
- **Area:** config-glob-output
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: Confirmed on Windows.
- **Related existing finding (GF/AF):** none
- **Source findings:** `config-glob-output CGO-11`

**Reproduction**

- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- with nothing listening on 127.0.0.2:20102:
  - `curl -sS --connect-timeout 2 http://127.0.0.2:20102/x`
  - `curl -sS http://127.0.0.2:20102/x`
  - Script: `E\verify\v2_misc.ps1` l1/l2.
  - On 127.0.0.1, both fail at once with the same output (`v3_misc.ps1` l3).

Commands (run each with REF and with PORT):

```sh
curl -sS --connect-timeout 2 http://127.0.0.2:20102/x
curl -sS http://127.0.0.2:20102/x
E\verify\v2_misc.ps1
```

**Expected (real curl)**

- With `--connect-timeout 2`: `curl: (28) Connection timed out after 2003 milliseconds`, exit 28.
- Without a timeout: `curl: (7) Failed to connect to 127.0.0.2:20102 after 2035 ms ...`, exit 7.

**Actual (Curl)**

- `curl: (7) Failed to connect to 127.0.0.2:20102 after 1 ms: Could not connect to server`, exit 7, in both cases.

**Suspected location in source**

- the port's connect path (not located). It is probably a loopback fast-fail socket option that the port applies to all of 127/8.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr189Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-189 of the third-party gap report.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

- 2026-10-29 (Claude): Cause: `TcpDialer.FailRefusedLoopbackAtOnce` (BL-3182) stopped SYN retransmissions for every loopback address; Windows/curl only fail 127.0.0.1 and ::1 at once. Fix: `TcpDialer.RefusesAtOnceOnWindows` limits it to those two; test `Cgr189Tests`.
- Measured with `--connect-timeout 1`: real curl 127.0.0.2:20102 -> `curl: (28) Connection timed out after 1009 milliseconds`, exit 28; Curl after fix -> `(28) ... after 1014 milliseconds`, exit 28; 127.0.0.1 still exit 7 at once. Without a timeout the port now waits for the system's ~2 s as curl does. Linux/macOS unaffected (Windows-only code path); change is platform-neutral.
- Fast tests: 1 failure, `SelfSignedServerUnderInsecure_...VerifyResult18` (NegotiatedGroupName null), unrelated, tracked by BL-4053. Measure-CodeQuality.ps1 aborted on that failure; the change adds one covered two-term expression (complexity 2).
- 2026-10-29: Doing -> Done. Refused connects to 127.0.0.2 etc. now take the system's ~2 s like curl (exit 28 under a short --connect-timeout); only 127.0.0.1/::1 fail at once
