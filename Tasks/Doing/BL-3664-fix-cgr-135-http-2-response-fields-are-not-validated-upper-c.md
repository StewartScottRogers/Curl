---
id: BL-3664
title: Fix CGR-135: HTTP/2 response fields are not validated: upper-case names, connection-specific headers, unknown, duplicate or misplaced pseudo-headers,...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2ResponseHead.cs, Curl.Protocol.Http.UnitTests/Cgr135Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3664 — Fix CGR-135: HTTP/2 response fields are not validated: upper-case names, connection-specific headers, unknown, duplicate or misplaced pseudo-headers,...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-135 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-135: HTTP/2 response fields are not validated: upper-case names, connection-specific headers, unknown, duplicate or misplaced pseudo-headers, :status 101, pseudo-headers in trailers, and trailers without END_STREAM are all accepted

- **Severity:** Medium (a malformed response is accepted and printed with exit 0 where curl fails; this only happens with non-conforming servers)
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches PORT-LINUX for `r_upper`, `r_connhdr` and `r2_status101`)
- **Confidence:** Confirmed. Detail: Confirmed (reproduced by the verifier; these are RFC 9113 8.2/8.3 MUSTs that nghttp2 enforces in every version).
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-2`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server: `h2srv.py --mode auto --scen <S>`.
- Command: `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x -w '\n[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_header} %{num_headers}]\n'`
- Values of S, with the verifier's cases:

  | S | Case | What the server sends |
  |---|---|---|
  | `upper` | `r_upper` | `X-Upper: v` |
  | `connhdr` | `r_connhdr` | `connection: close` |
  | `unkpseudo` | `r2_unkpseudo` | an unknown pseudo-header |
  | `dupstatus` | `r_dupstatus` | `:status` twice |
  | `pseudoafter` | `r_pseudoafter` | a regular header before `:status` |
  | `status101` | `r2_status101` | `:status 101` |
  | `trpseudo` | `r2_trpseudo` | a trailer containing `:status` |
  | `trnoes` | `r2_trnoes` | trailer HEADERS without END_STREAM, then DATA |
  | `cltwo` | `r2_cltwo` | `content-length` twice |
  | `resptegzip` | `r2_resptegzip` | `te: gzip` |

- The original tester also covered `keepalivehdr`, `upgradehdr`, `proxyconn`, `twoheaders` and `uppertrailer`, and the TLS variants `x_upper_tls`, `x_connhdr_tls` and `x_dupstatus_tls`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x -w '\n[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_header} %{num_headers}]\n'
```

**Expected (real curl)**

- Most cases exit 16 with `curl: (16) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [X-Upper], value: [v]` (or the equivalent field). No body is written.
- `pseudoafter` exits 8 with `Invalid HTTP header field ... [:status]`.
- `trnoes` exits 16 with `nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`.
- REF sends GOAWAY PROTOCOL_ERROR.

**Actual (Curl)**

- Exit 0. The invalid field is printed as-is (for example `X-Upper: v` or `connection: close`) and the body is written.
- `status101` prints `HTTP/2 101` followed by the 200 response.
- `trnoes` delivers `hello h2` + `more`, with the trailer `x-t: 1` glued to the body text (`morex-t: 1`).
- PORT sends a normal GOAWAY NO_ERROR `shutdown`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2ResponseHead.cs:23-53` (only `:status` is inspected, by `StatusOf`; no other field validation).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr135Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-135 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
