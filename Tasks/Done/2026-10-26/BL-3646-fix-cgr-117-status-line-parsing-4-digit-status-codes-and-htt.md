---
id: BL-3646
title: Fix CGR-117: Status-line parsing: 4+ digit status codes and HTTP/2 200 over HTTP/1.1 are refused with exit 8 where curl accepts them; %{http_code} is...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs, Curl.Protocol.Http.UnitTests/Cgr117Tests.cs, Curl.Protocol.Http.UnitTests/HttpStatusLineTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3646 — Fix CGR-117: Status-line parsing: 4+ digit status codes and HTTP/2 200 over HTTP/1.1 are refused with exit 8 where curl accepts them; %{http_code} is...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-117 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-117: Status-line parsing: 4+ digit status codes and `HTTP/2 200` over HTTP/1.1 are refused with exit 8 where curl accepts them; `%{http_code}` is 000 instead of the parsed digits; rejected heads are not written to `-i`/`-D`

- **Severity:** Medium
- **Area:** upstream-cases, writeout-verbose, http-responses, network-robustness
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [upstream-cases UC-2] Confirmed (on all four binaries).; [writeout-verbose WV-07] Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** GF-0098, GF-0012, GF-0083
- **Source findings:** `upstream-cases UC-2`, `writeout-verbose WV-07`, `http-responses HR-10`, `network-robustness NR-5`, `http-responses HR-12`

**Reproduction**

- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (http-responses):* EV = `SCR\evidence\http-responses`. Canned-response server: `dotnet run EV\server\server.cs -- 20400 EV\resp EV\logs`; `GET /NAME` returns the bytes of `EV\resp\NAME.bin` (if `EV\resp\NAME.opts` contains `keep`, the connection stays open). Differential: `EV\diff.ps1 -Name NAME -CArgs ... [-Opts keep]`; verifier `EV\verify\vdiff.ps1` (port 20420) driven by `EV\verify\vcases.ps1`. Linux: `EV\linux\server.py` serves the same fixtures (verifier: port 20460 via `EV\verify\linux\vrun.sh`; port 20450 in the original runs).
- *Setup (network-robustness):* EV = `SCR\evidence\network-robustness`. Windows server: `EV\srvbin\server.exe 21100 21101` (source `EV\srv\server.cs`); repro script `EV\repro_findings.sh` (uses the `d.sh` diff harness). Linux: `python3 EV/linux/srv.py 21150 21151`; repro `EV/linux/verify_lin.sh`. `H=http://127.0.0.1:21100` (21150 on Linux); `b64() { printf "$1" | base64 -w0 | sed 's/+/%2B/g; s/\//%2F/g; s/=/%3D/g'; }`; `$H/raw?b=<b64>` replies with exactly those bytes (`&keep=1` keeps the connection).
- **[upstream-cases UC-2]** Status codes longer than 3 digits, and `HTTP/2 200` on an HTTP/1.x connection, are rejected with exit 8; curl accepts them
  - Server reply `<STATUS>\r\nContent-Length: 6\r\n\r\n-foo-\n`, where STATUS is one of:
    - `HTTP/1.1 2000 OK`
    - `HTTP/1.1 2001`
    - `HTTP/1.0 2000 OK`
    - `HTTP/1.1 20000 OK`
    - `HTTP/1.1 4040 Not Found`
    - `HTTP/2 200`
  - Confs are `verify\conf\st_*.json`. Run `curl -s -S -w "[%{http_code}]" http://127.0.0.1:21351/x` (`verify\v1.sh`).
  - Control: `HTTP/1.1 200 OK` gives the same result on both.
- **[writeout-verbose WV-07]** Status codes of 4+ digits are refused with exit 8, and http_code is 000 where curl keeps the parsed digits
  - Run `curl -sS -w '|%{http_code}|%{exitcode}\n' http://127.0.0.1:20700/raw/<name>` for each canned response below.
  - `<name>` is `badcode` (`HTTP/1.1 99999 Huh`), `code2000` (`HTTP/1.1 2000`, body `ok`), `code1000` or `code099`.
- **[http-responses HR-10]** Status line "HTTP/2 200" (no reason phrase) over HTTP/1.1 fails with exit 8; curl accepts it as a 200
  - `st_v2b.bin` = `HTTP/2 200\r\nContent-Length: 2\r\n\r\nok`. Command: `curl -s -S http://127.0.0.1:20400/st_v2b`
- **[network-robustness NR-5]** `HTTP/2 200` status line on an HTTP/1.1 connection refused with `Version mismatch`
  - `curl -sSi -w '%{http_version} %{http_code}' "$H/raw?b=$(b64 'HTTP/2 200\r\nContent-Length: 1\r\n\r\na')"`.
- **[http-responses HR-12]** Rejected/odd status lines: head not written to -i/-D before the error, wrong %{http_code}, and "HTTP/1.1 2000" refused
  - Fixtures:
    - `st_0` = `HTTP/1.1 000 Zero\r\nContent-Length: 2\r\n\r\nok`
    - `st_099` (`099 Zero`), `st_1000` (`1000 Big`), `st_2000` (`2000 Big`)
    - `st_onlycrlf` (`\r\n\r\n`), `st_lf2` (`\nHTTP/1.1 200 OK...`)
  - Commands: `curl -s -S -i -w "%{http_code}" http://127.0.0.1:20400/st_0` (same for st_099, st_1000, st_onlycrlf) and `curl -s -S -i http://127.0.0.1:20400/st_2000`

Commands (run each with REF and with PORT):

```sh
# [upstream-cases UC-2]
curl -s -S -w "[%{http_code}]" http://127.0.0.1:21351/x
# [writeout-verbose WV-07]
curl -sS -w '|%{http_code}|%{exitcode}\n' http://127.0.0.1:20700/raw/<name>
# [http-responses HR-10]
curl -s -S http://127.0.0.1:20400/st_v2b
# [network-robustness NR-5]
curl -sSi -w '%{http_version} %{http_code}' "$H/raw?b=$(b64 'HTTP/2 200\r\nContent-Length: 1\r\n\r\na')"
# [http-responses HR-12]
curl -s -S -i -w "%{http_code}" http://127.0.0.1:20400/st_0
curl -s -S -i http://127.0.0.1:20400/st_2000
```

**Expected (real curl)**

- **[upstream-cases UC-2]** Exit 0, stdout `-foo-\n[200]` (`[404]` for 4040). REF-WIN 8.22.0 and REF-LINUX 8.18.0 agree.
- **[writeout-verbose WV-07]** 
  - `99999`: `|999|0`, exit 0.
  - `2000`: `ok|200|0`, exit 0.
  - `1000`: exit 8, http_code 100.
  - `099`: exit 1, http_code 099.
- **[http-responses HR-10]** exit 0, stdout `ok`. Same on REF 8.22 (Windows) and REF-LINUX 8.18, which has nghttp2.
- **[network-robustness NR-5]** *REF (8.22 Schannel and 8.18 OpenSSL):* exit 0, prints the head and `a`; -w `1 200`.
- **[http-responses HR-12]** 
  - st_0: writes the head, exit 1 (`Unsupported response code`), http_code `000`.
  - st_099: writes the head, exit 1, http_code `099`.
  - st_1000: writes the head, exit 8, http_code `100`.
  - st_2000: exit 0, head plus `ok`.
  - st_onlycrlf: writes `\r\n`, exit 1.
  - st_lf2: writes `\n`, exit 1.

**Actual (Curl)**

- **[upstream-cases UC-2]** Exit 8, stdout `[000]`, stderr `curl: (8) Invalid status line`. For `HTTP/2 200` the message is `curl: (8) Version mismatch (from HTTP/1 to HTTP/2)`.
- **[writeout-verbose WV-07]** 
  - `99999` and `2000`: exit 8 `Invalid status line`, http_code 000, and no body.
  - `1000`: exit 8 (matches REF) but http_code 000.
  - `099`: exit 1 (matches REF) but http_code 000.
- **[http-responses HR-10]** exit 8 `curl: (8) Version mismatch (from HTTP/1 to HTTP/2)` on both platforms.
- **[network-robustness NR-5]** exit 8 `curl: (8) Version mismatch (from HTTP/1 to HTTP/2)`, no output; -w `0 000`. `HTTP/2.0 200` matches.
- **[http-responses HR-12]** 
  - Same exit codes for st_0, st_099, st_1000, st_onlycrlf and st_lf2, but nothing is written to -i/-D.
  - http_code is `000` for st_099 (REF `099`) and for st_1000 (REF `100`).
  - st_2000: exit 8 `Invalid status line`, no output.
  - Same on Linux.

**Suspected location in source**

- **[upstream-cases UC-2]** 
  - `Curl.Protocol.Http.UnitLibrary\HttpStatusLine.cs:226-230`: a digit after the 3-digit code is refused.
  - `HttpStatusLine.cs:95-110`: `NamesHttp2OrHttp3` / `RefusalOverHttp1`. Its comment cites upstream test1704, but neither REF build refuses a plain `HTTP/2 200` reply.
- **[writeout-verbose WV-07]** `Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs` `Parse` (around line 59) rejects four-digit codes and does not keep the code on failure.
- **[http-responses HR-10]** `Curl.Protocol.Http.UnitLibrary\HttpStatusLine.cs:94` (`NamesHttp2OrHttp3`)
- **[network-robustness NR-5]** `Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs:95-109` (NamesHttp2OrHttp3 / RefusalOverHttp1). The source comment cites upstream test1704, but neither REF build behaves that way here.
- **[http-responses HR-12]** `HttpResponseHeadReader.cs` `ReadStatusLineAsync` (~509-527): the line is parsed before `builder.StartHead`/`ReportLine`, so a rejected line is never written. `HttpStatusLine.cs` `ParseHttp1` (~228) rejects a 4th digit.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr117Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 4) measured on Windows with REF-WIN (Git for Windows ucrt64 curl 8.22.0 Schannel) and Curl at this commit, each through `Record-CurlExchange.ps1`, command `curl -s -S -i -w "[%{http_code},%{http_version}]" http://127.0.0.1:<port>/x`, server reply `<STATUS>\r\nContent-Length: 6\r\n\r\n-foo-\n` (or `2\r\n\r\nok`). Results, identical for REF and Curl after the fix (stdout shown with \r\n escaped):
  - `HTTP/1.1 2000 OK`, `HTTP/1.1 2001`, `HTTP/1.1 20000 OK`: exit 0, head + `-foo-\n[200,1.1]`; `HTTP/1.0 2000 OK`: `[200,1]`; `HTTP/1.1 4040 Not Found`: `[404,1.1]`; `HTTP/1.1 99999 Huh`: head + `ok[999,1.1]`.
  - `HTTP/2 200` and `HTTP/3 200` (nothing after the code): exit 0, head + body, `[200,1]` (assumed HTTP/1.0 200), as NR-5 and HR-10 expect.
  - `HTTP/1.1 1000 Big`: exit 8 `curl: (8) Invalid status line`, the head written, `[100,1.1]`.
  - `HTTP/1.1 099 Zero` / `000 Zero`: exit 1 `curl: (1) Unsupported response code in HTTP response`, the whole head written, `[099,1.1]` / `[000,1.1]`.
  - `\r\n\r\n` / `\nHTTP/1.1 200 OK...`: exit 1 `curl: (1) Received HTTP/0.9 when not allowed`, stdout `\r\n` / `\n` then `[000,0]`.
  - Before the fix Curl gave exit 8 `Invalid status line` / `Version mismatch` for the accepted lines, `[000]` codes, and wrote nothing for the refused ones (the issue's Actual).
  - Also measured: a 1xx followed by a non-HTTP line (exit 8 Invalid status line), `HTTP/1.1 1000 Big\r\n\r\nHTTP/1.1 200 OK...` (exit 0, 200), `HTTP/1.1 200X` (exit 0, 200): REF and Curl agree. `XYZ\r\n\r\n` writes nothing on REF, so only an empty first line is written.
  - The fix changes only response parsing; the request bytes are the same GET as before. -D takes the same path as -i (`HeadBytesBeforeFailure`), so it gets the same bytes.
- Not changed, out of this issue: `HTTP/2 200 OK` (a blank after the code) over HTTP/1.1 still fails with exit 8 `Version mismatch (from HTTP/1 to HTTP/2)`, the nghttp2 build's answer (BL-2715, BL-3270); REF-WIN, built without nghttp2, exits 1 `Unsupported HTTP version (2.0) in response`.
- Fix: `HttpStatusLine.ParseHttp1` takes the first three digits as the code and no longer refuses a fourth digit (BL-1662's 1000 case still exits 8, now as curl does: an interim 100 then an invalid status line); a code below 100 carries its parsed line, and `HttpResponseHeadReader` reads the whole head before throwing it with the head bytes, so `-i`/`-D` and `%{http_code}` match. `NamesHttp2OrHttp3` needs a blank after the code. An empty first line refused as HTTP/0.9 carries its bytes for `-i`. `HttpStatusLineTests` had one row (`HTTP/3 100`, no blank) pinning the old rule; it now has a blank, plus a row for no blank, so that file was added to touches (no task in Doing names it).
- Focused tests: `Curl.Protocol.Http.UnitTests/Cgr117Tests.cs` (accepted codes, 1000, codes below 100, empty first line, `NamesHttp2OrHttp3`); the accepted-code rows failed before the fix with exit 8. Linux and macOS: the tests use scripted connections only, nothing depends on the platform.
- Build clean (0 warnings); fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-117 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. 4+ digit status codes read as their first three digits, HTTP/2 200 with nothing after the code taken as HTTP/1.0 200, codes below 100 and an empty first line write the head to -i/-D with curl's http_code
