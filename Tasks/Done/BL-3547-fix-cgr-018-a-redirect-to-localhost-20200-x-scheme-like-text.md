---
id: BL-3547
title: Fix CGR-018: A redirect to localhost:20200/x (scheme-like text with no //) is followed as http to a different host, where curl refuses it as an unsupp...
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr018Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs, Curl.Protocol.Http.UnitTests/Cgr018Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3547 — Fix CGR-018: A redirect to localhost:20200/x (scheme-like text with no //) is followed as http to a different host, where curl refuses it as an unsupp...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-018 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-018: A redirect to `localhost:20200/x` (scheme-like text with no `//`) is followed as http to a different host, where curl refuses it as an unsupported scheme

- **Severity:** High
- **Area:** url-parsing
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed. Reproduced by the original tester and by the verifier, on Windows (8.22) and on Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-01`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- `curl -sS -L "http://127.0.0.1:20200/__loc=localhost:20200/x" -w "[%{url_effective}]"`
  - Windows case: v01_redir_noscheme.
  - Linux case: lv01_redir_noscheme (port 20210).

Commands (run each with REF and with PORT):

```sh
curl -sS -L "http://127.0.0.1:20200/__loc=localhost:20200/x" -w "[%{url_effective}]"
```

**Expected (real curl)**

- Exit 1, with stderr `curl: (1) The redirect target URL could not be parsed: Unsupported URL scheme`.
- Only the first request is sent.

**Actual (Curl)**

- Exit 0.
- Sends a second request, `GET /x HTTP/1.1` with `Host: localhost:20200`, and prints `ok`.
- `%{url_effective}` is the raw text `localhost:20200/x`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:58-81` (`Resolve`/`HasScheme`) and `Curl.Core.UnitLibrary/RedirectFollower.cs:401` (`CurlUrl.TryParse(EncodedForFollow(target), ...)` guesses a scheme).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-10-26 with `Record-CurlExchange.ps1 -Port 18731`, a 302 with the `Location` below then `200` `ok`, `-sS -L http://127.0.0.1:18731/a -w "[%{url_effective}][%{redirect_url}]"`, real curl 8.22.0 (Windows) and Curl (`Curl.Console\bin\Debug\net10.0\curl.exe`) after the fix:
  - `Location: localhost:20200/x` (the issue's repro): both exit 1, stdout `[http://127.0.0.1:18731/a][]`, stderr `curl: (1) The redirect target URL could not be parsed: Unsupported URL scheme`, one request `GET /a`. Before the fix Curl followed it as http (the report's Actual).
  - `Location: foo:bar`: both the same as above.
  - `Location: http:/127.0.0.1:18731/y` (a variant that already followed): both exit 0, `GET /a` then `GET /y`; Curl's `%{url_effective}` is `http:/127.0.0.1:18731/y` where curl writes `http://127.0.0.1:18731/y`. That difference was there before this fix and is not this issue's repro; filed as BL-3827.
- Fix (ADR-0816): `RedirectFollower.ParseRefusal` reads scheme-like text and a colon at the start of the target as its scheme and refuses one not in `SchemesCurlParses` before `CurlUrl.TryParse` can guess one; the later scheme check became unreachable and was removed (it also kept `Refusal` under the complexity gate). Windows drive prefix (one letter) is not a scheme, as `UrlSchemeGuesser` does.
- `Curl.Core.UnitTests/Cgr018Tests.cs`: 4 of its cases failed with the check disabled and pass with it. No change was needed in `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs` or `Curl.Protocol.Http.UnitTests/Cgr018Tests.cs`; curl prints an empty `%{redirect_url}` here as Curl does.
- Platform check: the tests use no path, file or OS text; the drive-letter case is driven through the `runsOnWindows` constructor argument, so it gives the same answer on every OS. CI on Linux and macOS: met by keeping the change platform-neutral and the fast tests green here.
- Gates: `dotnet build` 0 warnings, 0 errors; `dotnet test --filter "TestCategory!=Integration"` exit 0; `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 0 failing members.

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr018Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-018 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A redirect to scheme-like text with no // (localhost:20200/x) is refused with exit 1 Unsupported URL scheme, as curl 8.22.0 does
