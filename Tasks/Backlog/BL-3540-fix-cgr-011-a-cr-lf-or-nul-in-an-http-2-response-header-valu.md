---
id: BL-3540
title: Fix CGR-011: A CR, LF or NUL in an HTTP/2 response header value goes into the HTTP/1 header parser, so a server can inject header lines such as Set-Co...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2ResponseHead.cs, Curl.Protocol.Http.UnitTests/Cgr011Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3540 — Fix CGR-011: A CR, LF or NUL in an HTTP/2 response header value goes into the HTTP/1 header parser, so a server can inject header lines such as Set-Co...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-011 (severity Critical): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-011: A CR, LF or NUL in an HTTP/2 response header value goes into the HTTP/1 header parser, so a server can inject header lines such as Set-Cookie

- **Severity:** Critical (header and cookie injection across the HTTP/2 to HTTP/1 boundary; RFC 9113 8.2.1 requires treating such a field as malformed)
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both. PORT-LINUX and PORT-WIN behave identically. REF was checked on Linux only.
- **Confidence:** Confirmed. Detail: Confirmed (reproduced by the tester and the verifier, and on PORT-WIN; the REF behaviour is an RFC 9113 check inside nghttp2, so it does not depend on the curl version).
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-1`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Script: `EV\inject_repro.sh` (output `EV\inject_repro.out`; verifier output `EV\verify\inject_repro.out`).
- Server (WSL): `python3 h2srv.py --port 21640 --scen inject --log inj.log`. It answers with `x-inj: a\r\nset-cookie: evil=1; Path=/` as a single literal HPACK field.
- Command: `curl -sS --http2-prior-knowledge -c jar.txt -D - -o /dev/null http://127.0.0.1:21640/x http://127.0.0.1:21640/y`
- Variant cases:
  - `r2_inject`, `x_inject_tls` (`-k -c jar.txt -D hdr.txt https://...`)
  - `r_crlfvalue` (`a\r\nb`)
  - `r2_lfvalue`, `x_lf_tls` (`a\nb`)
  - `r2_nulvalue` (`a\0b`)
  - `r2_loccrlf` (a 302 Location containing CRLF, with `-L`)

Commands (run each with REF and with PORT):

```sh
EV\inject_repro.sh
python3 h2srv.py --port 21640 --scen inject --log inj.log
curl -sS --http2-prior-knowledge -c jar.txt -D - -o /dev/null http://127.0.0.1:21640/x http://127.0.0.1:21640/y
```

**Expected (real curl)**

- nghttp2 rejects the field and curl ignores it without a message. No `x-inj` or `set-cookie` line is written, the jar is empty, request 2 carries no cookie, the body is delivered and the exit code is 0. `%{num_headers}` is 2.

**Actual (Curl)**

- `-D` prints `x-inj: a` and then `set-cookie: evil=1; Path=/`.
- The jar contains `127.0.0.1	FALSE	/	FALSE	0	evil	1`, and request 2 sends `cookie: evil=1` (seen on the wire, over both h2c and TLS).
- `%{num_headers}` is 4.
- `a\r\nb` and `a\nb` fail with `curl: (8) Header without colon` and no body is written.
- NUL fails with `curl: (8) Nul byte in header`.
- The `loccrlf` 302 case exits 8 where REF exits 0.
- PORT-WIN gives the same output (`EV\win\r2_inject`, `r2_lfvalue`, `r2_nulvalue`).

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2ResponseHead.cs:52-53` (`HeaderLines` joins `name: value\r\n` for every non-pseudo field and does not check the field).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr011Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-011 of the third-party gap report.
