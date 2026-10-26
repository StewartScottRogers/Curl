---
id: BL-3544
title: Fix CGR-015: Windows: non-ASCII command-line text is sent as UTF-8 for bodies, URLs, --data-urlencode, --url-query, -w and several non-HTTP protocols,...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr015Tests.cs, Curl.Console, Curl.Console.UnitTests/Cgr015Tests.cs, Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs, Curl.Output.UnitTests/Cgr015Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs, Curl.Protocol.Http.UnitTests/Cgr015Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3544 — Fix CGR-015: Windows: non-ASCII command-line text is sent as UTF-8 for bodies, URLs, --data-urlencode, --url-query, -w and several non-HTTP protocols,...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-015 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-015: Windows: non-ASCII command-line text is sent as UTF-8 for bodies, URLs, `--data-urlencode`, `--url-query`, `-w` and several non-HTTP protocols, while header-like options use the ANSI code page; REF uses the ANSI code page for all of them

- **Severity:** High
- **Area:** cli-options, url-parsing, http-requests, writeout-verbose, other-protocols, environment, upstream-cases
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: [cli-options CLIO-01] Confirmed (reproduced by the finder and re-run by the verifier); [http-requests HR-4] Confirmed: reproduced 2/2 on Windows.; [writeout-verbose WV-19] Confirmed against the designated REF (the Git ucrt64 build). Note that this depends on how curl was built (ANSI argv vs wmain/UTF-8), so a differently built curl.exe may behave like PORT. A `-w @file` with UTF-8 bytes matches.; [other-protocols OP-8] Confirmed (verifier re-ran this on Windows).
- **Related existing finding (GF/AF):** GF-0021
- **Source findings:** `cli-options CLIO-01`, `url-parsing UP-06`, `http-requests HR-4`, `writeout-verbose WV-19`, `other-protocols OP-8`, `environment ENV-07`, `upstream-cases UC-5`

Build note from the verifiers: REF is a non-UNICODE (ANSI argv) build. A UNICODE Windows curl (for example `C:\Windows\System32\curl.exe` 8.21, used only as a cross-check) converts every argument to UTF-8, headers included. The port matches neither build: it mixes UTF-8 and ANSI within one request. The fix must at least make the port consistent with itself; matching the designated oracle means using the ANSI code page everywhere REF does.

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- *Setup (environment):* EV = `SCR\evidence\environment`. Windows: `dotnet run EV\server.cs 21200` (echo server: returns the raw request as the body; `/bin` returns bytes 0..255) and `dotnet run EV\s2\server2.cs 21201` (same, used as a proxy). `EV\verify_windows.ps1` runs each case in isolated `aref`/`port` directories with HOME/USERPROFILE/APPDATA at an empty directory. Linux: `EV\linux\verify.sh` and `EV\linux\vtls.sh` (echo on 21210, proxy on 21211, `openssl s_server` on 21213).
- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- **[cli-options CLIO-01]** Non-ASCII arguments on Windows: PORT sends bodies, URLs and -w output as UTF-8; REF converts argv to the ANSI code page
  - *(echo server on 20000):* `curl -s -d "é" @U`; `curl -s "http://127.0.0.1:20000/é"`; `curl -s -o NUL -w "é" @U`; `curl -s --data-urlencode "é" @U`; `curl -s -d "中" @U`; `curl -s -o "中.txt" @U`; `curl --sílent @U`; `curl -sS "http://中文.localhost:20000/"`. Scripts: `repro/r01_unicode_win.sh`, `verify/v1_win.sh`.
- **[url-parsing UP-06]** Windows: non-ASCII command-line arguments are taken as UTF-8, where the reference build reads them in the ANSI code page
  - `curl -sS "http://127.0.0.1:20200/café"` (v06_argv_ansi)
  - `curl -sS "http://127.0.0.1:2020<U+FF10>/a"`, where U+FF10 is the full-width digit zero (v06b_port_fw)
- **[http-requests HR-4]** On Windows, request bodies and URLs built from command-line text use UTF-8, while REF uses the ANSI code page (cp1252). Header options use ANSI in both, so the port is inconsistent with itself.
  - On a cp1252 system:
    - `curl -sS -d é http://127.0.0.1:20335/a`
    - `curl -sS --data-urlencode é http://127.0.0.1:20335/a`
    - `curl -sS http://127.0.0.1:20335/é`
    - `curl -sS --url-query q=é http://127.0.0.1:20335/a`

    The cases are v4_* in `E\verify.json`. The finder's cases are in `E\c4.json`, `E\c19.json` and `E\c24.json`, which also cover `--json`, `--request-target`, `-T ü.txt` and CJK file names.
- **[writeout-verbose WV-19]** Non-ASCII literal text in -w on Windows is written as UTF-8; REF writes ANSI code page bytes
  - `curl -s -o NUL -w 'café ü €\n' http://127.0.0.1:20700/`, run from Git Bash.
- **[other-protocols OP-8]** On Windows, non-ASCII command-line text reaches the wire as UTF-8 in several places where curl sends ANSI (cp1252) bytes
  - `EV\enc.cases` and `EV\enc2.cases`. Commands:
    - `curl -s -S -d "héllo" mqtt://127.0.0.1:21001/t` (also with `--data-binary` and `--data-raw`)
    - `curl -s -S --data-urlencode "héllo" mqtt://127.0.0.1:21001/t`
    - `curl -s -S "gopher://127.0.0.1:21001/0/é"`
    - `curl -s -S "dict://127.0.0.1:21001/d:é"`
    - `curl -s -S -u u:p -X "XFOO é" pop3://127.0.0.1:21001/` (also with `€`)
    - `curl -s -S -d "€" mqtt://127.0.0.1:21001/t`
- **[environment ENV-07]** Non-ASCII argument text is sent as UTF-8 for `-d`/`--data-urlencode`/URL path/`-w` (and `--json`, `--data-raw`, `--data-binary`, `--variable`), but as ANSI for `-H`/`-A`/`-b`/`-e`/`-X`/`-F`/`-u`
  - `curl -s -d x=é http://127.0.0.1:21200/echo`
  - `curl -s http://127.0.0.1:21200/é`
  - `curl -s --data-urlencode x=é ...`
  - `curl -s -o NUL -w é ...`
  - Compare with `curl -s -H "X-U: é" ...`
  - Verifier cases: v-d-latin, v-url-latin, v-urlenc-latin, v-w-latin and v-H-latin.
- **[upstream-cases UC-5]** Windows: non-ASCII command-line text is sent as UTF-8 in the URL, -d, --data-urlencode, --url-query and -w, where REF-WIN uses the ANSI code page. PORT still uses ANSI for -H
  - `verify\vwin.ps1` (cases `v_cp_*`, port 21370, canned 200 reply). Each command is run natively on Windows:
    - `curl -s "http://127.0.0.1:21370/“é"`
    - `curl -s -H "X-A: café" -d "v=é" http://127.0.0.1:21370/x`
    - `curl -s --data-urlencode v=é --url-query q=é http://127.0.0.1:21370/x`
    - `curl -s -o NUL -w "[é]" http://127.0.0.1:21370/x`

Commands (run each with REF and with PORT):

```sh
# [cli-options CLIO-01]
curl -s -d "é" @U
curl -s "http://127.0.0.1:20000/é"
curl -s -o NUL -w "é" @U
curl -s --data-urlencode "é" @U
curl -s -d "中" @U
curl -s -o "中.txt" @U
curl --sílent @U
curl -sS "http://中文.localhost:20000/"
# [url-parsing UP-06]
curl -sS "http://127.0.0.1:20200/café"
curl -sS "http://127.0.0.1:2020<U+FF10>/a"
# [http-requests HR-4]
curl -sS -d é http://127.0.0.1:20335/a
curl -sS --data-urlencode é http://127.0.0.1:20335/a
curl -sS http://127.0.0.1:20335/é
curl -sS --url-query q=é http://127.0.0.1:20335/a
# [writeout-verbose WV-19]
curl -s -o NUL -w 'café ü €\n' http://127.0.0.1:20700/
# [other-protocols OP-8]
curl -s -S -d "héllo" mqtt://127.0.0.1:21001/t
curl -s -S --data-urlencode "héllo" mqtt://127.0.0.1:21001/t
curl -s -S "gopher://127.0.0.1:21001/0/é"
curl -s -S "dict://127.0.0.1:21001/d:é"
curl -s -S -u u:p -X "XFOO é" pop3://127.0.0.1:21001/
curl -s -S -d "€" mqtt://127.0.0.1:21001/t
# [environment ENV-07]
curl -s -d x=é http://127.0.0.1:21200/echo
curl -s http://127.0.0.1:21200/é
curl -s --data-urlencode x=é ...
curl -s -o NUL -w é ...
curl -s -H "X-U: é" ...
# [upstream-cases UC-5]
curl -s "http://127.0.0.1:21370/“é"
curl -s -H "X-A: café" -d "v=é" http://127.0.0.1:21370/x
curl -s --data-urlencode v=é --url-query q=é http://127.0.0.1:21370/x
curl -s -o NUL -w "[é]" http://127.0.0.1:21370/x
```

**Expected (real curl)**

- **[cli-options CLIO-01]** everything goes through code page 1252. `-d é` sends 0xE9 with Content-Length 1. The URL is sent as `/%E9`. `-w` writes 0xE9 and `--data-urlencode` gives `%E9`. `-d 中` sends `?`, and `-o 中.txt` creates `_.txt`. The unknown-option message echoes the ANSI byte. `http://中文.localhost` gives exit 3 "URL rejected: No host present".
- **[url-parsing UP-06]** 
  - Sends `GET /caf%E9`. `%{url_effective}` is written as CP1252 bytes.
  - The full-width digit is best-fit mapped to `0`, so the request connects to port 20200 and exits 0.
- **[http-requests HR-4]** 
  - `-d é` sends body `\xe9` with `Content-Length: 1`
  - `--data-urlencode é` sends `%E9`
  - the URL path becomes `/%E9`
  - `--url-query` gives `?q=%E9`
- **[writeout-verbose WV-19]** `63 61 66 E9 20 FC 20 80 0D 0A` (cp1252)
- **[other-protocols OP-8]** *REF behaviour (cp1252 bytes):* 
  - MQTT payload `68 E9 6C 6C 6F`
  - `--data-urlencode` payload `h%E9llo`
  - gopher selector `/\xE9`
  - dict `DEFINE ! \xE9`
  - POP3 `XFOO \xE9` and `XFOO \x80`
  - MQTT `€` sent as `80`
- **[environment ENV-07]** cp1252 everywhere:
  - `-d` sends body `x=` E9 (Content-Length 3).
  - The URL request line is `GET /%E9`.
  - `--data-urlencode` sends `x=%E9`.
  - `-w` writes E9.
  - `-H` sends E9.
- **[upstream-cases UC-5]** 
  - URL: `GET /%93%E9`
  - Body: `v=\xE9` with Content-Length: 3
  - Query: `?q=%E9`, and the urlencoded body is `v=%E9`
  - -w writes byte E9
  - -H sends E9

**Actual (Curl)**

- **[cli-options CLIO-01]** the `-H` header value matches REF (ANSI), but bodies, URLs and `-w` are UTF-8. `-d é` sends C3 A9 with Content-Length 2, the URL becomes `/%C3%A9`, `-w` writes C3 A9 and `--data-urlencode` gives `%C3%A9`. `-d 中` sends E4 B8 AD, `-o 中.txt` creates `中.txt`, and the message reads `--sílent` in UTF-8. The IDN URL succeeds with `Host: xn--fiq228c.localhost:20000`. The same argv is therefore encoded two ways depending on the option.
- **[url-parsing UP-06]** 
  - Sends `GET /caf%C3%A9`, and `%{url_effective}` is written in UTF-8.
  - The full-width digit gives exit 3, `Port number was not a decimal number between 0 and 65535`.
- **[http-requests HR-4]** 
  - `-d é` sends `\xc3\xa9` with `Content-Length: 2`
  - `--data-urlencode é` sends `%C3%A9`
  - the URL path becomes `/%C3%A9`
  - `--url-query` gives `?q=%C3%A9`
  - The control case `-H "X: é"` sends `\xe9` in both binaries.
- **[writeout-verbose WV-19]** `63 61 66 C3 A9 20 C3 BC 20 E2 82 AC 0D 0A` (UTF-8)
- **[other-protocols OP-8]** *PORT behaviour (UTF-8 bytes):* 
  - `68 C3 A9 6C 6C 6F`
  - `h%C3%A9llo`
  - `/\xC3\xA9`
  - `DEFINE ! \xC3\xA9`
  - `XFOO \xC3\xA9` and `XFOO \xE2\x82\xAC`
  - `E2 82 AC`
  - The PORT is not consistent with itself: SMTP `-d`, `-u` and SMTP `-X` use cp1252 and match REF.
- **[environment ENV-07]** 
  - `-d` sends `x=` C3 A9 (4).
  - The URL request line is `GET /%C3%A9`.
  - `--data-urlencode` sends `x=%C3%A9`.
  - `-w` writes C3 A9.
  - `-H` sends E9, the same as REF.
- **[upstream-cases UC-5]** 
  - URL: `GET /%E2%80%9C%C3%A9`
  - Body: `v=\xC3\xA9` with Content-Length: 4
  - Query: `?q=%C3%A9`, and the urlencoded body is `v=%C3%A9`
  - -w writes C3 A9
  - -H sends E9, the same as REF. PORT is therefore inconsistent with itself.

**Notes**

- **[url-parsing UP-06]** *Verifier note:* the Microsoft-shipped `C:\Windows\System32\curl.exe` is a UNICODE build. Checked for information only, not as the oracle, it sends `GET /caf%C3%A9` like PORT. The difference therefore comes from the non-UNICODE mingw REF build. It is a mismatch with the designated oracle, but arguably not a defect.
- **[http-requests HR-4]** *Note:* REF here is the MSYS2 ucrt64 (ANSI) build. Unicode builds of curl for Windows convert argv to UTF-8 and would send what the port sends. Even so, the port does not match the REF oracle, and it mixes the two encodings within one request.
- **[environment ENV-07]** *Build note:* REF is a non-UNICODE build. A UNICODE build (`C:\Windows\System32\curl.exe` 8.21) sends UTF-8 for both `-d` and `-H`. The port therefore matches neither build: it uses UTF-8 for data, URL and write-out, and ANSI for headers.
- **[upstream-cases UC-5]** *Verifier note:* This difference depends on how curl was built. REF-WIN (Git for Windows ucrt64) is an ANSI build. A Unicode Windows build of curl converts every argument to UTF-8, -H included. PORT matches neither build completely.

**Suspected location in source**

- **[cli-options CLIO-01]** `Curl.Cli.UnitLibrary/CommandLineOptions.cs:2377` (`AppendPostData` uses `Encoding.UTF8.GetBytes`); `CommandLineOptionTable.cs` (the data-urlencode and json rows).
- **[url-parsing UP-06]** `Curl.Console` entry point (the arguments are encoded as UTF-8).
- **[http-requests HR-4]** 
  - `Curl.Cli.UnitLibrary/CommandLineOptions.cs:2377` (`AppendPostData` uses `Encoding.UTF8`)
  - `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:836`, `:972` and `:979`
  - `Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs:26`
  - `HttpRequestHeadFormatter.cs:228`
- **[writeout-verbose WV-19]** `Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs:101`
- **[other-protocols OP-8]** the per-protocol conversion from argument to bytes: the MQTT payload, `GopherSelector`, the DICT request and the POP3 control channel.
- **[environment ENV-07]** `Curl.Cli.UnitLibrary` `CommandLineOptions.AsWireText` (ANSI) is applied only to the header-like options.
- **[upstream-cases UC-5]** argument decoding in Curl.Console / Curl.Cli.UnitLibrary.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr015Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-015 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
