---
id: BL-3560
title: Fix CGR-031: REFUSED_STREAM, and GOAWAY with a last-stream-id below the request, are not retried on a new connection; REF retries and succeeds
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Protocol.Http.UnitTests/Cgr031Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3560 — Fix CGR-031: REFUSED_STREAM, and GOAWAY with a last-stream-id below the request, are not retried on a new connection; REF retries and succeeds

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-031 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-031: REFUSED_STREAM, and GOAWAY with a last-stream-id below the request, are not retried on a new connection; REF retries and succeeds

- **Severity:** High (servers commonly send GOAWAY or REFUSED_STREAM during graceful shutdown or overload, and curl hides this from the user)
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\x_rst7first`, `EV\win\x_gafirst`)
- **Confidence:** Probable. Detail: Probable (reproduced by the tester and the verifier on Linux and PORT-WIN; the REF retry logic lives in curl's own code, which can only be checked against 8.18)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-5`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scens, which act on connection 1 only:
  - `rst7first`: RST_STREAM REFUSED_STREAM.
  - `gafirst`: GOAWAY with last=0 and NO_ERROR, then close.
  - `gafirstia`: the same with INTERNAL_ERROR.
- Command: `curl -sS --http2-prior-knowledge http://127.0.0.1:P/x -w '\n[%{http_version} %{response_code} %{num_connects}]\n'`. TLS form: `curl -sSk https://127.0.0.1:P/x`.
- Cases: `x_rst7first`, `x_rst7first_tls`, `x_gafirst`, `x_gafirstia`, `x_gafirst_tls`.
- Always-refusing variants (`rst7`, `gabefore0`): REF gives up after 5 retries.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge http://127.0.0.1:P/x -w '\n[%{http_version} %{response_code} %{num_connects}]\n'
curl -sSk https://127.0.0.1:P/x
```

**Expected (real curl)**

- opens a second connection, re-sends the request, prints `hello h2` and exits 0 with `[2 200 2]`. If the server always refuses, REF exits 56 with `curl: (56) Connection died, tried 5 times before giving up`.

**Actual (Curl)**

- no retry, `[0 000 1]`.
  - REFUSED_STREAM: exit 92, `curl: (92) HTTP/2 stream 1 was not closed cleanly: REFUSED_STREAM (err 7)`.
  - GOAWAY: exit 56, `curl: (56) Failure when receiving data from the peer`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2StreamConnection.cs:375` (every `Http2StreamResetException` becomes exit 92, and `Http2GoAwayException` becomes 56). `Http3StreamConnection.cs:300` has a request-rejected retry, but HTTP/2 does not.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr031Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-031 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
