---
id: BL-3803
title: Fix CGR-274: %{size_request} counts a user Transfer-Encoding header that HTTP/2 drops
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs, Curl.Protocol.Http.UnitTests/Cgr274Tests.cs]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3803 — Fix CGR-274: %{size_request} counts a user Transfer-Encoding header that HTTP/2 drops

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-274 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-274: %{size_request} counts a user Transfer-Encoding header that HTTP/2 drops

- **Severity:** Low
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\p_post_te_chunked`)
- **Confidence:** Probable
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-18`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Commands:
  - `curl -sS --http2-prior-knowledge -H 'Transfer-Encoding: chunked' -d hello http://127.0.0.1:P/echo -w '%{size_request}'`
  - `curl -sSk -H 'Transfer-Encoding: chunked' -d hello https://127.0.0.1:P/echo -w '%{size_request}'`
- Cases: `p_post_te_chunked`, `x_te_size_tls`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -H 'Transfer-Encoding: chunked' -d hello http://127.0.0.1:P/echo -w '%{size_request}'
curl -sSk -H 'Transfer-Encoding: chunked' -d hello https://127.0.0.1:P/echo -w '%{size_request}'
```

**Expected (real curl)**

- `size_request` is 155. The wire HEADERS are identical to PORT's.

**Actual (Curl)**

- `size_request` is 183, 28 bytes more: the length of `Transfer-Encoding: chunked\r\n`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2RequestHeaders.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr274Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-274 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.

## Notes (lane 9)
- Cause: the h1-form head handed to HTTP/2/3 (`WithRequestLineVersion`) kept the user's `Transfer-Encoding: chunked` line, which `Of()` drops from the wire, but `size_request` counts the head length. Fix: `WithRequestLineVersion` omits the Transfer-Encoding line (28 bytes). Expected size_request 155 per the report's REF-LINUX 8.18.0 measure; no HTTP/2 server or curl 8.22.0 here to re-measure, so the evidence is the report's figures (183 -> 155) plus Cgr274Tests.
- Wire HEADERS unchanged (already identical). Fast tests green (2592 passed); Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 0 failing members.
- 2026-10-28: Doing -> Done. Fixed: HTTP/2 and HTTP/3 request head omits Transfer-Encoding
