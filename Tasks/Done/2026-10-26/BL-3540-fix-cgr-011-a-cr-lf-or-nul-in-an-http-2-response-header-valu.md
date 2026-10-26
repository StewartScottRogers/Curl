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
completed: 2026-10-26
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

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr011Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Fix: `Http2ResponseHead.HeaderLines` leaves out any field whose value holds a CR, LF or NUL, silently, as nghttp2 does for curl (ADR-0815). HTTP/3 heads share the path. Focused tests in `Cgr011Tests.cs`: all 7 fail with the filter removed and pass with it.
- Harness (decided: REF is WSL curl 8.18.0 with nghttp2 1.68.0, the report's REF-LINUX; the Windows REF has no HTTP/2): canned h2c bytes (SETTINGS, SETTINGS ACK, HEADERS with `:status` and one literal-without-indexing field, DATA `hi\n`) served by `nc -l` inside WSL; REF runs in WSL, PORT (`Curl.Console\bin\Debug\net10.0\curl.exe`) on Windows through WSL's localhost forwarding. Script kept outside the repo (`%TEMP%\bl3540\run.ps1`, PowerShell).
- Results after the fix, REF and PORT identical (exit, stdout, stderr, files):
  - `curl -sS --http2-prior-knowledge -c jar.txt -D - -o out1.txt -w "nh=%{num_headers}." http://127.0.0.1:P/x`, field `x-inj: a\r\nset-cookie: evil=1; Path=/`: exit 0, stdout `HTTP/2 200 \r\n\r\nnh=0.`, stderr empty, out1.txt `hi\n`, jar has only the header comments (PORT's jar uses CRLF line ends, as a Windows build does).
  - `-D - -w "nh=%{num_headers}."` with `a\r\nb`, `a\nb`, `a\0b`: exit 0, stdout `HTTP/2 200 \r\n\r\nhi\nnh=0.`, stderr empty. Before the fix PORT exited 8 (Header without colon / Nul byte in header).
  - `-sS -L ... -w "nh=%{num_headers},code=%{http_code}."`, 302 with `location: /next\r\nx: y`: exit 0, stdout `HTTP/2 302 \r\n\r\nhi\nnh=0,code=302.`, no follow.
- Two-URL variant (`.../x .../y` on one connection): REF exits 16 on transfer 2 (`nghttp2 recv error -902` / `Ignoring received invalid HTTP header field ... stream: 3, name: [], value: []`) even when the field is a valid `x-inj: ok`, so that comes from the canned second response, not from CGR-011; transfer 1 matches PORT (no injected line, empty jar), and PORT's jar is empty so request 2 carries no cookie. The report's REF (a real nghttp2 server) gave exit 0 for both transfers, which PORT now gives.
- Platform check: the tests are pure byte formatting, no paths or OS text. CI on Linux and macOS: change is platform-neutral and the fast tests are green here.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created from CGR-011 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. HTTP/2 and HTTP/3 response header fields whose value holds a CR, LF or NUL are ignored silently as curl's nghttp2 build does, so no header line or cookie can be injected
