---
id: BL-3800
title: Fix CGR-271: A 70 KB custom request header is sent over HTTP/2 in HEADERS+CONTINUATION; REF fails with exit 55
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs, Curl.Protocol.Http.UnitTests/Cgr271Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed:
---
# BL-3800 — Fix CGR-271: A 70 KB custom request header is sent over HTTP/2 in HEADERS+CONTINUATION; REF fails with exit 55

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-271 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-271: A 70 KB custom request header is sent over HTTP/2 in HEADERS+CONTINUATION; REF fails with exit 55

- **Severity:** Low (PORT arguably does better here; this is a difference, not a failure)
- **Area:** http2-linux
- **Platform(s):** Linux. Detail: Linux verified (the Windows command line is too long for the 70 KB argument)
- **Confidence:** Probable. Detail: Probable (the REF limit may depend on the version)
- **Related existing finding (GF/AF):** GF-0075
- **Source findings:** `http2-linux H2L-15`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scen `ok`. Command: `curl -sS --http2-prior-knowledge -H "X-Big: $(head -c 70000 /dev/zero | tr '\0' b)" http://127.0.0.1:P/x -w '[%{size_request}]'`. Case: `z_huge_req_hdr`. With 20 KB (`z_big_req_hdr`) the two match.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -H "X-Big: $(head -c 70000 /dev/zero | tr '\0' b)" http://127.0.0.1:P/x -w '[%{size_request}]'
```

**Expected (real curl)**

- exit 55, `curl: (55) Failed sending data to the peer`, `size_request` 65536.

**Actual (Curl)**

- exit 0. It sends HEADERS of 16384 bytes and 3 CONTINUATION frames and receives `hello h2`; `size_request` is 70087.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2RequestHeaders.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2) (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr271Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-271 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
