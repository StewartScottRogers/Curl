---
id: BL-3650
title: Fix CGR-121: HTTP/2 against a peer that does not speak HTTP/2 (--http2-prior-knowledge to an HTTP/1 server, or an h2c upgrade that never sends SETTING...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Protocol.Http.UnitTests/Cgr121Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3650 — Fix CGR-121: HTTP/2 against a peer that does not speak HTTP/2 (--http2-prior-knowledge to an HTTP/1 server, or an h2c upgrade that never sends SETTING...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-121 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-121: HTTP/2 against a peer that does not speak HTTP/2 (`--http2-prior-knowledge` to an HTTP/1 server, or an h2c upgrade that never sends SETTINGS) exits 55 instead of 16

- **Severity:** Medium
- **Area:** upstream-cases, http2-linux
- **Platform(s):** Linux. Detail: [upstream-cases UC-8] Linux (REF-WIN has no HTTP/2); [http2-linux H2L-9] Linux verified (PORT-LINUX). PORT-WIN uses the same code path; the Windows re-run was not valid because `winsrv.sh` has no h1 mode.
- **Confidence:** Confirmed. Detail: [upstream-cases UC-8] Confirmed (exit code).; [http2-linux H2L-9] Probable (the exit code may depend on the curl and nghttp2 version, and was checked only against 8.18)
- **Related existing finding (GF/AF):** GF-0083, GF-0098
- **Source findings:** `upstream-cases UC-8`, `http2-linux H2L-9`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- **[upstream-cases UC-8]** An HTTP/2 failure caused by a peer that is not speaking HTTP/2 exits 55 instead of 16
  - `verify\v2.sh`.
    - Server `verify\conf\h1801.json` replies `HTTP/1.1 101 Switching!\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\nlies!\n`. Run `curl http://127.0.0.1:21354/1801 --http2`.
    - Server `ok.json`: run `curl http://127.0.0.1:21354/x --http2-prior-knowledge`.
- **[http2-linux H2L-9]** --http2-prior-knowledge against a server that does not speak HTTP/2 exits 55 instead of 16
  - Server `--mode h1`, scen `ok` (replies HTTP/1.1) or `h1close` (closes), or TLS with ALPN `http/1.1` only.
  - Commands:
    - `curl -sS --http2-prior-knowledge http://127.0.0.1:P/x`
    - `curl -sk --http2-prior-knowledge https://127.0.0.1:P/x`
  - Cases: `h1_pk`, `h1_pk_close`, `tls_onlyh1_pk` (the tester also ran `h1_pk_garbage`, `h1_tls_pk`, `t_onlyh1_pk` and `x_onlyh1_pk`).

Commands (run each with REF and with PORT):

```sh
# [upstream-cases UC-8]
curl http://127.0.0.1:21354/1801 --http2
curl http://127.0.0.1:21354/x --http2-prior-knowledge
# [http2-linux H2L-9]
curl -sS --http2-prior-knowledge http://127.0.0.1:P/x
curl -sk --http2-prior-knowledge https://127.0.0.1:P/x
```

**Expected (real curl)**

- **[upstream-cases UC-8]** Exit 16, `curl: (16) Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.` Upstream 8.22.0 test1801 also expects 16, so this does not depend on the version.
- **[http2-linux H2L-9]** 
  - Exit 16, `curl: (16) Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.`
  - When the server closes: exit 16, `curl: (16) Error in the HTTP2 framing layer`.

**Actual (Curl)**

- **[upstream-cases UC-8]** Exit 55 with the same message, in both cases.
- **[http2-linux H2L-9]** 
  - Exit 55 with the same text.
  - When the server closes: exit 55, `curl: (55) Send failure: Broken pipe`.
  - The port's own comment says exit 55 was measured (BL-3372), but REF-LINUX 8.18 gives 16 in every variant.

**Notes**

- **[upstream-cases UC-8]** *Additional difference (Probable):* In the h2c upgrade request, PORT sends `Upgrade`, then `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA`, then `Connection`. That is byte-identical to REF-LINUX 8.18.0. Upstream 8.22.0 test1801 expects `Connection: Upgrade, HTTP2-Settings` first and `HTTP2-Settings: AAMAAABkAARAAAAAAAIAAAAA`. No 8.22.0 binary with HTTP/2 was available to confirm this on the wire.

**Suspected location in source**

- **[upstream-cases UC-8]** `Curl.Protocol.Http.UnitLibrary\Http2Session.cs:426` (SendError=55).
- **[http2-linux H2L-9]** `Curl.Protocol.Http.UnitLibrary\Http2Session.cs:426` (`new HttpTransferException(CurlExitCode.SendError, HttpTransferMessages.Http2SettingsExpected)`)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr121Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-121 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
