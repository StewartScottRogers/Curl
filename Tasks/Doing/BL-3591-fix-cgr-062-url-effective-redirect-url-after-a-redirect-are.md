---
id: BL-3591
title: Fix CGR-062: %{url_effective} / %{redirect_url} after a redirect are the raw Location text instead of curl's normalised URL (also for unusual absolute...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr062Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs, Curl.Protocol.Http.UnitTests/Cgr062Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3591 — Fix CGR-062: %{url_effective} / %{redirect_url} after a redirect are the raw Location text instead of curl's normalised URL (also for unusual absolute...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-062 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-062: `%{url_effective}` / `%{redirect_url}` after a redirect are the raw Location text instead of curl's normalised URL (also for unusual absolute Locations)

- **Severity:** Medium
- **Area:** url-parsing, http-stateful
- **Platform(s):** Both (Windows and Linux). Detail: [http-stateful HS-9] Both (the `file:` case differs on Windows only)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0008
- **Source findings:** `url-parsing UP-20`, `http-stateful HS-9`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- **[url-parsing UP-20]** `%{url_effective}` after a redirect is the raw Location text instead of the normalised URL
  - `curl -sS -L "http://127.0.0.1:20200/__loc=https:/x" -w "%{url_effective}"` (v20_redir_eff)
  - `curl -sS -L "http://127.0.0.1:20200/__loc=/a/%252e%252e/b" -w "%{url_effective}"` (v20b_redir_eff_dots)
  - Linux cases: lv20_*.
- **[http-stateful HS-9]** %{redirect_url} and %{url_effective} differ for unusual absolute Location values
  - `/a/b` returns `302` with `Location: X`. Run `curl -sS[L] -w '%{http_code} %{num_redirects} %{url_effective} %{redirect_url}\n' http://127.0.0.1:20500/a/b`.
  - Try X = `http:/b`, `file:///c:/windows/win.ini`, `http://\xE9.invalid/b` (a raw E9 byte) and `http:`.
  - Cases: `EV\cases\loc-scheme-path`, `loc-file-scheme-noL`, `loc-unicode-host-noL`, `loc-scheme-only`.

Commands (run each with REF and with PORT):

```sh
# [url-parsing UP-20]
curl -sS -L "http://127.0.0.1:20200/__loc=https:/x" -w "%{url_effective}"
curl -sS -L "http://127.0.0.1:20200/__loc=/a/%252e%252e/b" -w "%{url_effective}"
# [http-stateful HS-9]
curl -sS[L] -w '%{http_code} %{num_redirects} %{url_effective} %{redirect_url}\n' http://127.0.0.1:20500/a/b
```

**Expected (real curl)**

- **[url-parsing UP-20]** `https://x/` and `http://127.0.0.1:20200/b`.
- **[http-stateful HS-9]** 
  - `http:/b` becomes `http://b/`.
  - The file Location becomes `file://c:/windows/win.ini`.
  - The host byte E9 is written back as the single byte E9. Linux 8.18 fails with exit 3 instead.
  - `http:` fails with `Unsupported number of slashes following scheme`.

**Actual (Curl)**

- **[url-parsing UP-20]** `https:/x` and `http://127.0.0.1:20200/a/%2E%2E/b`, although the request line that was sent is `GET /b`.
- **[http-stateful HS-9]** 
  - `http:/b` is reported unchanged as `http:/b` (both exit 6 with -L).
  - The file Location stays `file:///c:/windows/win.ini`. On Linux this case matches REF.
  - The host byte E9 is reported as UTF-8 `C3 A9`.
  - `http:` fails with `Port number was not a decimal number between 0 and 65535`.

**Suspected location in source**

- **[url-parsing UP-20]** `Curl.Core.UnitLibrary/RedirectFollower.cs` (url_effective comes from the target string, not from the parsed CurlUrl).
- **[http-stateful HS-9]** `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:91` (`NormalizeAbsolute`; the header is read as Latin-1).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [ ] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] Repro variants in the issue that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr062Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-062 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
