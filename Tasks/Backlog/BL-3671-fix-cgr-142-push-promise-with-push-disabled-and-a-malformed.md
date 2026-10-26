---
id: BL-3671
title: Fix CGR-142: PUSH_PROMISE (with push disabled) and a malformed or missing :status give different exit codes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Http2.UnitLibrary/Http2Connection.cs, Curl.Http2.UnitTests/Cgr142Tests.cs, Curl.Protocol.Http.UnitLibrary/Http2ResponseHead.cs, Curl.Protocol.Http.UnitTests/Cgr142Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3671 — Fix CGR-142: PUSH_PROMISE (with push disabled) and a malformed or missing :status give different exit codes

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-142 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-142: PUSH_PROMISE (with push disabled) and a malformed or missing :status give different exit codes

- **Severity:** Medium
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_push`, `r_statusbad`, `r_nostatus`)
- **Confidence:** Probable
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-12`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scens: `push`, `statusbad` (`:status 2x0`), `statusspace`, `statusshort`, `nostatus`.
- Command: `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x`
- Cases: `r_push`, `r_statusbad`, `r_nostatus` (the tester also ran `r2_statusspace`, `r2_statusshort`, `x_push_tls` and `x_nostatus_tls`).

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x
```

**Expected (real curl)**

- push: exit 92, `curl: (92) [HTTP2] [1] received invalid frame: FRAME[PUSH_PROMISE, len=60, hend=1], error -505: Protocol error`.
- Bad :status: exit 16, `Invalid HTTP header field was received: frame type: 1, stream: 1, name: [:status], value: [2x0]`.
- Missing :status: exit 8, `curl: (8) nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`.

**Actual (Curl)**

- push: exit 16, `curl: (16) nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`.
- Bad or missing :status: exit 92, `curl: (92) HTTP/2 stream 1 was not closed cleanly: PROTOCOL_ERROR (err 1)`. PORT sends RST_STREAM instead of GOAWAY.

**Suspected location in source**

- `Curl.Http2.UnitLibrary\Http2Connection.cs:581` (`ReceivePushPromise`); `Curl.Protocol.Http.UnitLibrary\Http2ResponseHead.cs:23` (`StatusOf`)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr142Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-142 of the third-party gap report.
