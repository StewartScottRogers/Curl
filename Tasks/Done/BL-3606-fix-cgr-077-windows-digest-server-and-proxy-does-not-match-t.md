---
id: BL-3606
title: Fix CGR-077: Windows Digest (server and proxy) does not match the SSPI reference: challenges REF refuses with exit 94 are answered, and answer bytes d...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/DigestAuthenticator.cs, Curl.Authentication.UnitLibrary/DigestChallengeParameters.cs, Curl.Authentication.UnitLibrary/DigestQuoting.cs, Curl.Authentication.UnitTests/Cgr077Tests.cs, Curl.Authentication.UnitLibrary/DigestChallenge.cs, Curl.Authentication.UnitLibrary/DigestSspiChallenge.cs, Curl.Authentication.UnitTests/DigestAuthenticatorTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3606 — Fix CGR-077: Windows Digest (server and proxy) does not match the SSPI reference: challenges REF refuses with exit 94 are answered, and answer bytes d...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-077 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-077: Windows Digest (server and proxy) does not match the SSPI reference: challenges REF refuses with exit 94 are answered, and answer bytes differ for userhash, escaping, case, charset, white space, long nonces, `DOMAIN\user`, empty and non-ASCII user names

- **Severity:** Medium
- **Area:** http-stateful, upstream-cases, proxy-connect
- **Platform(s):** Windows. Detail: [http-stateful HS-14] Windows (the Linux part depends on the curl version); [proxy-connect PC-10] Windows. On Linux the port matches 8.18 for userhash (VL20).
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0003, GF-0034, GF-0006
- **Source findings:** `http-stateful HS-12`, `http-stateful HS-13`, `http-stateful HS-14`, `upstream-cases UC-6`, `proxy-connect PC-10`

**Reproduction**

- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- *Setup (upstream-cases):* EV = `SCR\evidence\upstream-cases`. Cross-platform runs: `EV\verify\diff.sh`, driven in WSL by `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- bash "/mnt/c/Users/Stewart Rogers/AppData/Local/Temp/claude/Z--repos-Curl-ThirdPartyReview/3796612b-55d9-44ec-9722-80e35c3282a1/scratchpad/evidence/upstream-cases/verify/sync.sh" vN.sh` (N = 1..4), against the canned recording server `EV\verify\srv.py` (configs in `EV\verify\conf\`). Windows-native runs: `EV\verify\vwin.ps1` (wraps `harness\Diff-Curl.ps1` and `Record-CurlExchange.ps1`).
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- **[http-stateful HS-12]** Windows Digest: the SSPI build's refusals (exit 94) are missing, and PORT answers unusual challenges
  - `/p` returns 401 with the challenge below, then 200 `authed`.
  - Commands: `curl -sS --digest -u user:pass 'http://127.0.0.1:20500/p?x=1'`, and the same with `-X PATCH -d zz`.
  - Challenges: `algorithm=FOO`; `qop="auth-int"`; `qop="foo"`; no nonce; a trailing comma.
  - Cases: `EV\cases\dg-unknown-alg`, `dg-auth-int`, `dg-qop-unknown`, `dg-nononce`, `dg-trailing-comma`, `dg-X`. Script: `EV\c08_digest.ps1`.
- **[http-stateful HS-13]** Windows Digest answer bytes differ from the SSPI build for several challenge forms
  - the same setup as HS-12. Challenges:
    - (a) `userhash=true` (MD5)
    - (b) `realm="te\"st", nonce="a\\bc"`
    - (c) `DIGEST REALM=... ALGORITHM=md5`
    - (d) `charset=UTF-8`
    - (e) `Digest    realm = "testrealm" ,  nonce = "abc123" ,qop="auth"`
    - (f) a 3000-character nonce
    - Cases: `EV\cases\dg-userhash-md5`, `dg-escaped`, `dg-case`, `dg-charset`, `dg-extra-ws`, `dg-long-nonce`.
- **[http-stateful HS-14]** Windows Digest with a "DOMAIN\user", empty or non-ASCII user name does not match SSPI
  - `/p` returns 401 `Digest realm="testrealm", nonce="abc123", qop="auth"`, then 200.
  - Commands: `curl -sS --digest -u 'dom\user:pass' http://127.0.0.1:20500/p`, then the same with `-u ':pass'` and with `-u 'usér:pass'`.
  - Cases: `EV\cases\dgu-dom_user_pass`, `dgu-us_er_pass`, `dgu-_pass`, `dgu-us_r_pass`.
- **[upstream-cases UC-6]** Windows Digest auth does not match REF-WIN's SSPI digest (URI quoting, backslash in user name, duplicated challenge parameters, CR/LF in the user name)
  - `verify\vwin.ps1` (cases `v_dg*`, port 21371). The server answers with two connections: first `401` with `WWW-Authenticate: Digest realm="testrealm", nonce="1053604145"`, then `200`.
    - (a) `curl -s "http://127.0.0.1:21371/64\"" -u testuser:testpass --digest`
    - (b) `curl -s "http://%5cuser%22:password@127.0.0.1:21371/1229" --digest`
    - (c) Challenge `Digest realm="testrealm", algorithm=MD5, algorithm=MD5, nonce="1", nonce="2"`, then `curl -s http://127.0.0.1:21371/1437 -u testuser:testpass --digest`
    - (d) `curl -s -S http://127.0.0.1:21371/3221 -u "hello<LF><CR>:pass" --digest`
- **[proxy-connect PC-10]** Windows: Digest challenges with userhash=true or qop="auth-int" are answered differently from the Schannel/SSPI reference
  - run `srv 20600 log 'H;W:HTTP/1.1 407 X\r\nProxy-Authenticate: Digest realm="p", nonce="n1", userhash=true\r\nContent-Length: 0\r\n\r\n;H;OK'`, then `curl -s -x http://127.0.0.1:20600 --proxy-digest -U us:pw http://example.test/x`. For the second case use `qop="auth-int"` instead of `userhash=true`. Verifier cases: v16 and v17.

Commands (run each with REF and with PORT):

```sh
# [http-stateful HS-12]
curl -sS --digest -u user:pass 'http://127.0.0.1:20500/p?x=1'
EV\c08_digest.ps1
# [http-stateful HS-14]
curl -sS --digest -u 'dom\user:pass' http://127.0.0.1:20500/p
# [upstream-cases UC-6]
curl -s "http://127.0.0.1:21371/64\"" -u testuser:testpass --digest
curl -s "http://%5cuser%22:password@127.0.0.1:21371/1229" --digest
curl -s http://127.0.0.1:21371/1437 -u testuser:testpass --digest
curl -s -S http://127.0.0.1:21371/3221 -u "hello<LF><CR>:pass" --digest
# [proxy-connect PC-10]
srv 20600 log 'H;W:HTTP/1.1 407 X\r\nProxy-Authenticate: Digest realm="p", nonce="n1", userhash=true\r\nContent-Length: 0\r\n\r\n;H;OK'
curl -s -x http://127.0.0.1:20600 --proxy-digest -U us:pw http://example.test/x
```

**Expected (real curl)**

- **[http-stateful HS-12]** prints `curl: (94) An authentication function returned an error` and exits 94.
- **[http-stateful HS-13]** 
  - (a) `username="user"` with no userhash; the server check passes.
  - (b) `nonce="a\bc"`.
  - (c) `algorithm=MD5`.
  - (d) appends `,charset=utf-8`.
  - (e) answers with `realm=""` and gets the 200.
  - (f) keeps cnonce, nc and `qop="auth"`.
- **[http-stateful HS-14]** 
  - `dom\user` gives `username="user",realm="dom"`.
  - `:pass` gives `curl: (67) Login denied` and exit 67.
  - `usér` sends the raw byte E9.
- **[upstream-cases UC-6]** 
  - (a) `uri="/64""` (the quote is sent raw).
  - (b) `username="user\""` (SSPI drops the leading `\`), response=ed551b...
  - (c) Exit 94 and no second request.
  - (d) Exit 94, `curl: (94) An authentication function returned an error`.
- **[proxy-connect PC-10]** for userhash it sends `username="us"` with no userhash parameter. For an auth-int-only challenge it exits 94 and sends no second request.

**Actual (Curl)**

- **[http-stateful HS-12]** exits 0.
  - For auth-int, an unknown qop, a trailing comma and PATCH, it answers with Digest and prints `authed`.
  - For an unknown algorithm or a missing nonce, it prints the 401 body `denied`.
- **[http-stateful HS-13]** 
  - (a) `username="f7b4a687..."` with `userhash=true`; the server check fails.
  - (b) `nonce="a\\bc"`.
  - (c) `algorithm=md5`.
  - (d) no charset.
  - (e) sends no Authorization header and prints `denied`.
  - (f) drops qop, cnonce and nc, which makes it an RFC 2069 answer.
- **[http-stateful HS-14]** 
  - `dom\user` gives `username="dom\\user",realm="testrealm"`.
  - `:pass` sends `username=""` and exits 0 (`authed`).
  - `usér` sends `username="us%E9r"`.
- **[upstream-cases UC-6]** 
  - (a) `uri="/64\""` (escaped).
  - (b) `username="\\user\""`, response=f2694d...
  - (c) Sends the retry with nonce="2" and exits 0, printing the body.
  - (d) Sends `username="hello%0A%0D"` and exits 0.
- **[proxy-connect PC-10]** for userhash it sends `username="e6585ea998f7e371f4493fd74d84b791",...,userhash=true`. For auth-int it sends `qop="auth-int"` and exits 0 with `hello`.

**Notes**

- **[http-stateful HS-14]** *Linux:* PORT sends `us%C3%A9r` where REF 8.18 sends raw UTF-8. The port says it copies 8.21's quoting, so this part depends on the curl version and is not claimed.
- **[upstream-cases UC-6]** *Verifier note:* PORT's values in (b), (c) and (d) match upstream's non-SSPI expectations, but its header layout (no space after commas) copies SSPI. The result is a hybrid that no real curl build produces, and it differs from REF-WIN.

**Suspected location in source**

- **[http-stateful HS-12]** `Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:70` refuses only the algorithms that SSPI does not answer. See also `DigestChallengeParameters.cs`.
- **[http-stateful HS-13]** `Curl.Authentication.UnitLibrary/DigestAuthenticator.cs:146` (`FormatSspiValue`); `DigestChallengeParameters.cs`.
- **[http-stateful HS-14]** `Curl.Authentication.UnitLibrary/DigestQuoting.cs:22-31`; `DigestAuthenticator.cs:120`.
- **[upstream-cases UC-6]** `Curl.Authentication.UnitLibrary` (Digest on Windows).
- **[proxy-connect PC-10]** `Curl.Authentication.UnitLibrary\DigestAuthenticator.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr077Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Touches widened (nobody in Doing on origin/work/dark-factory names these files): `Curl.Authentication.UnitLibrary/DigestChallenge.cs` (a `TryReadFirst` that takes the parameter reader, and `SendsUtf8Charset`), the new `Curl.Authentication.UnitLibrary/DigestSspiChallenge.cs` (SSPI's challenge reader), and `Curl.Authentication.UnitTests/DigestAuthenticatorTests.cs` (its SSPI userhash test pinned the userhash that SSPI never sends). `DigestChallengeParameters.cs` was not changed.
- Measured 2026-10-26 with `Record-CurlExchange.ps1` (two connections: 401 with the challenge and body `denied`, then 200 `authed`; `curl -sS --digest -u <user> http://127.0.0.1:<port>/p?x=1`, plus `-X PATCH -d zz`, `-d zz`, the URL `/64"`, the URL user `%5cuser%22:password`) against REF = curl 8.22.0 Schannel (Git ucrt64) and PORT = Curl.Console Debug. Proxy: `curl -s -x http://127.0.0.1:20531 --proxy-digest -U us:pw http://example.test/x` with a 407 carrying `userhash=true` or `qop="auth-int"`. 47 cases in all: HS-12 (unknown algorithm, auth-int, qop foo, no nonce, trailing comma, PATCH), HS-13 (userhash, escaped, case, charset, extra blanks, 3000-char nonce), HS-14 (`dom\user`, `:pass`, `usér`), UC-6 (a)-(d), PC-10 (both) and 25 more forms (repeated realm, qop, opaque, unknown key, algorithm and nonce, empty element, key without value, unquoted values, charset with opaque, other charset, nonce/opaque/realm with escapes, no realm, tab in user, `dom/user`, `dom\`, `\user`, quote and `~%` in user, SHA-256, quoted algorithm, `auth-int,auth`, `AUTH`, MD5-sess with and without qop, stale, RFC 2069).
- REF results (PORT after the fix is the same): exit 94, `curl: (94) An authentication function returned an error`, one request, for unknown algorithm, SHA-256, auth-int only, qop foo, no nonce, trailing comma, two nonces, empty element, key without value, CR/LF or tab in the user name (UC-6 c, d). Exit 67 `curl: (67) Login denied` for `:pass`. PATCH answers as GET does (exit 0, `authed`). userhash ignored (server and proxy: `username="us"`, no userhash). `realm="te\"st"` and `nonce="a\\bc"` on the wire give `realm="te\"st",nonce="a\bc"`; nonce and opaque are sent unescaped (`nonce="a"b"`). `ALGORITHM=md5` gives `algorithm=MD5`. `charset=UTF-8` appends `,charset=utf-8` after opaque. `realm = "testrealm"` with blanks gives `realm=""`, 200. 3000-char nonce keeps cnonce, nc and `qop="auth"`. `dom\user` and `dom/user` give `username="user",realm="dom"`; `usér` sends byte E9; `/64"` gives `uri="/64""`; `%5cuser%22` gives `username="user\""`, response ed551ba287723feffb063cafe9c43daa. Every response hash of both builds (68 answers) was checked to verify for its fields (`v.ps1`). Before the fix, PORT differed in every way the issue's Actual lists (re-measured; also the 25 extra forms differed where REF refuses or SSPI escapes differently).
- Two differences remain, both outside the issue's repros, recorded in ADR-0824: (1) REF sends `nc=00000002` in its first answer when the 401's connection was closed (a kept connection, `-Script`, gives `nc=00000001`); PORT always sends 1 - filed as BL-3869. (2) MD5-sess without qop: REF answers with a hash not reproducible from the bytes it sends; PORT now fails with exit 94 instead of sending nothing (before: exit 0, `denied`).
- Focused tests: `Cgr077Tests.cs` (41 cases). Each pins REF's measured answer, which PORT before the fix did not give (the before-fix PORT measurement above), so each failed before the fix; all pass after it. Curl.Authentication.UnitTests: 977 passed, 6 skipped.
- Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary: 0 failing members, after splitting three methods the first run put past complexity 10.
- Platform: the unit tests are pure string work with an injected encoding (Latin-1) and `matchesSspiBuild: true`; nothing depends on the OS, so they pass on Linux and macOS too.
- Decision: ADR-0824 (decided by Claude under Stewart's delegation).

## Log

- 2026-10-26: Created from CGR-077 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Windows Digest (server and proxy) now reads and answers challenges as curl 8.22.0's SSPI does; every CGR-077 repro matches
