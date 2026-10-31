---
id: BL-3664
title: Fix CGR-135: HTTP/2 response fields are not validated: upper-case names, connection-specific headers, unknown, duplicate or misplaced pseudo-headers,...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3667, BL-3668, BL-3979]
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2ResponseHead.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Record-CurlExchange.ps1, Curl.Original.Protocol.Http.UnitTests/Cgr135Tests.cs]
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

- `Curl.Original.Protocol.Http.UnitLibrary\Http2ResponseHead.cs:23-53` (only `:status` is inspected, by `StatusOf`; no other field validation).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr135Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 3, first claim): touches widened. The fix cannot stay in Http2ResponseHead.cs: the stream must enqueue a partial head, then fail the connection with GOAWAY PROTOCOL_ERROR, which lands in Http2StreamConnection.cs (held by BL-3667) and needs a locked GOAWAY send in Http2Session.cs (held by BL-3668); HttpTransferMessages.cs gets the new message; Record-CurlExchange.ps1 gained -Http2Frames to measure (no other task names either). Requeued until BL-3667 and BL-3668 are Done. The -Http2Frames extension is left uncommitted in the stash.
- Recorder extension (Record-CurlExchange.ps1, stashed): `-Http2Frames 'H :status: 200|X-Upper: v','D! hello h2\n'`: with -Http2Origin, answers with these frames: 'H' HEADERS / 'D' DATA, '!' = END_STREAM; HEADERS fields '|'-separated, each sent as a literal with a new name exactly as written. REF runs through `wsl.exe -e curl` with `-ListenAddress 172.26.96.1` (the WSL gateway; 127.0.0.1 gives exit 7). Driver used: a pwsh loop over the scenarios with `-Http2Origin prior-knowledge -Response 'HTTP/1.1 200 OK\r\n\r\n' -CurlArgs '-sS','--http2-prior-knowledge','-i',<url>,'-w','\n[%{http_version} %{response_code} %{num_connects} %{size_download} %{size_header} %{num_headers}]\n'`.
- REF-LINUX (curl 8.18.0, nghttp2 1.68) measured, every failing case also sends `GOAWAY last stream 0 error 1` (PROTOCOL_ERROR); passing cases GOAWAY error 0. Message form: `curl: (16) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [<name>], value: [<value>]`. stdout = what was received before the bad field (status line and earlier valid lines, no blank line after them), then the -w line:
  - upper (`:status 200|X-Upper: v`): 16, name [X-Upper] value [v]; stdout `HTTP/2 200 \r\n` + `\n[2 200 1 0 13 0]\n`.
  - connhdr (`connection: close`), keepalivehdr (`keep-alive: timeout=5`), upgradehdr (`upgrade: h2c`), proxyconn (`proxy-connection: keep-alive`), resptegzip (`te: gzip`), unkpseudo (`:foo: bar`), dupstatus (second `:status: 200`): each 16 naming that field; stdout `HTTP/2 200 \r\n` + `[2 200 1 0 13 0]`.
  - cltwo (`content-length: 9` twice): 16 naming the second; stdout `HTTP/2 200 \r\ncontent-length: 9\r\n` + `[2 200 1 0 32 1]`.
  - pseudoafter (`x-a: 1|:status: 200`): exit 8 (!), message names [:status] value [200]; stdout only `\n[0 000 1 0 0 0]\n`.
  - status101 (`:status: 101`, then a 200 head): 16 naming [:status] [101]; stdout only `[0 000 1 0 0 0]`.
  - trpseudo (head, DATA `hello h2\n`, trailer `H! :status: 200`): 16 naming [:status] [200]; stdout full head `HTTP/2 200 \r\n\r\nhello h2\n` + `[2 200 1 10 15 0]`. uppertrailer (trailer `X-T: 1`): same, naming [X-T] [1].
  - twoheaders (two `:status: 200` blocks, the second without END_STREAM, then DATA): 16 naming [:status] [200]; stdout `HTTP/2 200 \r\n\r\n` + `[2 200 1 0 15 0]` (field check comes before the END_STREAM check).
  - trnoes (head, DATA `hello h2`, `H x-t: 1` without END_STREAM, `D! more`): `curl: (16) nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`; stdout `HTTP/2 200 \r\n\r\nhello h2` + `[2 200 1 8 15 0]`.
  - Controls that pass (exit 0): `te: trailers`; a good head with `x-a: 1` and trailer `x-t: 1` (trailer printed after the body, `hello h2\nx-t: 1\r\n`).
- Plan for the next claim (nghttp2 1.68 `nghttp2_http_on_header` rules): add `Http2ResponseHead.InvalidFieldIndex(fields, isTrailer)` giving the first invalid field or -1: a pseudo-header is invalid in trailers, after a regular field, when it is not `:status`, a second `:status`, or `:status 101`; a regular name with an upper-case letter is invalid; `connection`, `keep-alive`, `proxy-connection`, `transfer-encoding`, `upgrade` invalid; `te` invalid unless `trailers` (case-insensitive); a second `content-length` or a non-digit one invalid; a regular field whose value holds CR/LF/NUL stays silently ignored first (CGR-011), as does a name with other non-token characters. Keep a bad `:status` value (not 101) on its measured exit 92 path. In `Http2StreamConnection.ReceiveHeadersAsync`: check fields first; on an invalid one, for a head whose first field is `:status`, enqueue the status line plus the valid lines before it with no blank line, store the failure as pending and throw it once `received` is drained (ReadAsync), so -i prints the partial head; send GOAWAY PROTOCOL_ERROR (last stream 0) via a new locked `Http2Session` method that also records connectionFailure; exit 8 (WeirdServerReply) when the head's first field is a regular one, else 16. Then a trailer HEADERS (after the final head) without END_STREAM fails with exit 16 `Http2ShutsDownConnection(ProtocolError)` and GOAWAY 1, its fields not written. Leave HTTP/3 alone (Http3StreamConnection also uses Format/StatusOf). Focused tests in Cgr135Tests.cs: InvalidFieldIndex cases plus one Http2StreamConnection replay (upper case -> exit 16 message, partial head).

- 2026-10-28 (lane 2, second claim): fix written, left uncommitted for the shift's stash. `Http2ResponseHead.IndexOfInvalidField(fields, isTrailer)` (the plan above; it also took over the content-length check from `Http2StreamConnection.IndexOfInvalidContentLength`, now removed); `Http2StreamConnection.ReceiveHeadersAsync` checks fields first and calls `RejectInvalidFieldAsync` (writes the status line plus the lines before the field with no empty line, GOAWAY PROTOCOL_ERROR, exit 8 when a regular field came before any `:status`, else 16), then shuts the connection down for trailers without END_STREAM; `ShutDownMalformedAsync` takes the exit code. No change was needed in Http2Session.cs or HttpTransferMessages.cs (BL-3667 already gave `session.Frames.SendGoAwayAsync`). `Cgr135Tests.cs`: 15 replay scenarios, 2 passing controls, 10 `IndexOfInvalidField` rows; Curl.Original.Protocol.Http.UnitTests 2522 passed, 0 failed.
- Recorder: the stashed -Http2Frames conflicted with BL-3912's -Http2Answer/-Http2NoSettings; merged as a `Frames` property beside theirs. Its DATA unescape was a no-op (`Replace("\r", "\r")`), so REF's DATA `hello h2\n` was 10 bytes with a literal backslash-n: that is why REF's trpseudo/uppertrailer show size_download 10. Fixed to `Replace("\r", "\r")`; the PORT runs below send 9 bytes.
- PORT (this checkout, Windows, `& .\Record-CurlExchange.ps1` in one pwsh session - `pwsh -File` passes -CurlArgs as one string): upper, connhdr, keepalivehdr, upgradehdr, proxyconn, resptegzip, unkpseudo, dupstatus: exit 16 with REF's message naming the field, stdout `HTTP/2 200 \r\n` + `\n[2 200 1 0 13 0]\n`, GOAWAY last 0 error 1 - match. pseudoafter: exit 8, `[0 000 1 0 0 0]` - match. status101: 16, `[0 000 1 0 0 0]` - match. trpseudo, uppertrailer: 16, `HTTP/2 200 \r\n\r\nhello h2\n` + `[2 200 1 9 15 0]` - match (9 vs REF's 10 is the recorder escape above). twoheaders: 16, `[2 200 1 0 15 0]` - match. trnoes: 16 `nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, `HTTP/2 200 \r\n\r\nhello h2` + `[2 200 1 8 15 0]` - match. Controls te: trailers and a trailer ending the stream: exit 0, GOAWAY error 0 - match. cltwo: exit 16, message and head bytes match, but `[2 200 1 0 32 0]` against REF's `[2 200 1 0 32 1]`: num_headers does not count a head failed part way. That is in HttpResponseHeadReader/HttpProtocolHandler, outside touches: filed BL-3979 and requeued behind it.
- Next claim: once BL-3979 is Done, apply the stash, re-run the cltwo case only, run Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary once (not yet run), then build, fast tests, Done.

## Log

- 2026-10-26: Created from CGR-135 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Needs Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs (held by BL-3667) and Http2Session.cs (held by BL-3668); REF measured and plan recorded in Notes
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Waits on BL-3979 (num_headers for an HTTP/2 head failed part way, cltwo); every other CGR-135 repro now matches REF, fix stashed
- 2026-10-28: Backlog -> Deferred. Split into BL-4004 and BL-4005 (over its cost cap)
