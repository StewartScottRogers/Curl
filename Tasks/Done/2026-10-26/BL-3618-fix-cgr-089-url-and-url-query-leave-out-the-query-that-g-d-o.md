---
id: BL-3618
title: Fix CGR-089: %{url} and %{url.query} leave out the query that -G -d or --url-query appended
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CLAUDE.md, Curl.Console.UnitTests/Cgr089Tests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Output.UnitTests/Cgr089Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3618 — Fix CGR-089: %{url} and %{url.query} leave out the query that -G -d or --url-query appended

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-089 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-089: `%{url}` and `%{url.query}` leave out the query that `-G -d` or `--url-query` appended

- **Severity:** Medium
- **Area:** writeout-verbose, url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [writeout-verbose WV-08] Confirmed (Windows and Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `writeout-verbose WV-08`, `url-parsing UP-07`

**Reproduction**

- *Setup (writeout-verbose):* EV = `SCR\evidence\writeout-verbose`. Windows server: `cd EV\srv && dotnet run wvsrv.cs -- 20700 EV\resp EV\req.log` (HTTP on 127.0.0.1/::1:20700, a mini FTP server on 20701, TLS on 20702; `/raw/<name>` serves `EV\resp\<name>.txt`; other routes `/code/N`, `/redir/N`, `/hang/ms`, `/close` (empty reply), `/ka`, `/echo`, `/p`). Linux: `EV\linux\srv.py` on 20710. One-line repros for every finding: `EV\repro_findings.sh` (Windows) and `EV\linux\verify_linux.sh`.
- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- **[writeout-verbose WV-08]** %{url} / %{url.query} ignore the query that -G -d or --url-query add
  - `curl -s -o NUL -w '%{url}|%{url.query}\n' -G -d a=b http://127.0.0.1:20700/p`
  - `curl -s -o NUL -w '%{url}|%{url.query}\n' --url-query x=1 'http://127.0.0.1:20700/p?y=2'`
- **[url-parsing UP-07]** `%{url}` leaves out the query that `-G`/`-d` or `--url-query` appended
  - `curl -sS -G -d a=b "http://127.0.0.1:20200/x" -w "%{url}"` (v07_G_url)
  - `curl -sS --url-query a=b "http://127.0.0.1:20200/x" -w "%{url}"` (v07b_uq_url)
  - Linux cases: lv07_*.

Commands (run each with REF and with PORT):

```sh
# [writeout-verbose WV-08]
curl -s -o NUL -w '%{url}|%{url.query}\n' -G -d a=b http://127.0.0.1:20700/p
curl -s -o NUL -w '%{url}|%{url.query}\n' --url-query x=1 'http://127.0.0.1:20700/p?y=2'
# [url-parsing UP-07]
curl -sS -G -d a=b "http://127.0.0.1:20200/x" -w "%{url}"
curl -sS --url-query a=b "http://127.0.0.1:20200/x" -w "%{url}"
```

**Expected (real curl)**

- **[writeout-verbose WV-08]** 
  - `-G -d`: `http://127.0.0.1:20700/p?a=b|a=b`
  - `--url-query`: `http://127.0.0.1:20700/p?y=2&x=1|y=2&x=1`
- **[url-parsing UP-07]** `http://127.0.0.1:20200/x?a=b`

**Actual (Curl)**

- **[writeout-verbose WV-08]** 
  - `-G -d`: `http://127.0.0.1:20700/p|`
  - `--url-query`: `http://127.0.0.1:20700/p?y=2|y=2`
  - `%{url_effective}` and `urle.*` are correct.
- **[url-parsing UP-07]** `http://127.0.0.1:20200/x`. The request line is correct.

**Suspected location in source**

- **[writeout-verbose WV-08]** `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs`: `url` and `url.*` are taken before `-G` and `--url-query` are applied.
- **[url-parsing UP-07]** `Curl.Console/CurlCommandRunner.cs:2834-2836` (`%{url}` is taken from the URL before `QueryUrl.Append`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr089Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 8): Fixed in `Curl.Console/CurlCommandRunner.cs` (`WriteOutGivenUrl`): when `-G`/`--url-query` appends a query, `%{url}` and `%{url.*}` read the URL with it, normalised by `UrlEffective` as `%{url_effective}` is. `TransferWriteOutVariables` needed no change (it parses whatever URL it is handed, schemeless included), so the Output files in `touches` stay untouched. Added `Curl.Console/CLAUDE.md` to `touches` to correct its "`%{url}` keeps the URL as typed" sentence; no task in Doing on origin/work/dark-factory names it.
- Measured on curl 8.21.0 Schannel (`C:\Windows\System32\curl.exe`, the platform's curl), port 1 closed (`-s -o NUL -w '%{url}|%{url.query}|%{url_effective}\n'`):
  - `-G -d a=b http://127.0.0.1:1/p` -> `http://127.0.0.1:1/p?a=b|a=b|http://127.0.0.1:1/p?a=b`
  - `--url-query x=1 'http://127.0.0.1:1/p?y=2'` -> `http://127.0.0.1:1/p?y=2&x=1|y=2&x=1|...`
  - `-G -d 'a=%2f' 'HTTP://127.0.0.1:1/a/../p#frag'` -> `http://127.0.0.1:1/p?a=%2F#frag|a=%2F|...` (normalised, fragment kept)
  - `-G -d a=b 127.0.0.1:1/p` -> `127.0.0.1:1/p?a=b|a=b|http://127.0.0.1:1/p?a=b` (no guessed scheme in `%{url}`; curl 8.18.0 mingw prints it with `http://`, 8.21.0 Schannel without; matched the platform's curl)
  - `-G -d a=b 'http://a b/p'` (rejected) -> `http://a b/p||` (as typed, no query)
  - without `-G`, `'HTTP://127.0.0.1:1/a/../p'` -> `HTTP://127.0.0.1:1/a/../p|` (as typed, unchanged)
  Every case is pinned in `Curl.Console.UnitTests/Cgr089Tests.cs`; 5 of its 7 cases failed with the fix removed and all 7 pass with it.
- The issue's four repro commands through `Record-CurlExchange.ps1` against a 200 `ok` responder on port 18961, ref = curl 8.21.0 Schannel vs Curl: exit code 0 for both; stdout identical (`http://127.0.0.1:18961/p?a=b|a=b`, `http://127.0.0.1:18961/p?y=2&x=1|y=2&x=1`, `okhttp://127.0.0.1:18961/x?a=b` twice); stderr empty for both; no files written; request bytes identical except `User-Agent: curl/8.21.0` vs `curl/8.22.0`, the version Curl reports (the report's normalisation). Variants that already matched (`%{url_effective}`, `urle.*`, the request line) still match.
- No ADR: no design choice beyond matching the platform's curl as measured.
- `dotnet build` clean; fast tests green (0 failed). `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members, worst CRAP 10.
- CI on Linux and macOS: the tests use fake handlers and no paths, OS text or certificates, so they are platform-neutral.

## Log

- 2026-10-26: Created from CGR-089 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. %{url} and %{url.query} include the query -G -d or --url-query appended, as curl does
