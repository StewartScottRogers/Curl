---
id: BL-3574
title: Fix CGR-045: An invalid or non-HTTP --doh-url makes every transfer fail with exit 6, even to IP literals
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UnusableDohUrlResolver.cs, Curl.Engine.UnitTests/Cgr045Tests.cs, Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3574 — Fix CGR-045: An invalid or non-HTTP --doh-url makes every transfer fail with exit 6, even to IP literals

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-045 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-045: An invalid or non-HTTP --doh-url makes every transfer fail with exit 6, even to IP literals

- **Severity:** Medium
- **Area:** cli-options
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-11`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- `curl -sS --doh-url "a b" @U`; `curl -sS --doh-url ftp://127.0.0.1:20002/ @U`.

Commands (run each with REF and with PORT):

```sh
curl -sS --doh-url "a b" @U
curl -sS --doh-url ftp://127.0.0.1:20002/ @U
```

**Expected (real curl)**

- an IP literal needs no lookup, so the request is sent (exit 0).

**Actual (Curl)**

- `curl: (6) Could not resolve host: 127.0.0.1`, exit 6.

**Suspected location in source**

- `Curl.Engine.UnitLibrary/UnusableDohUrlResolver.cs:16` (returns no addresses for every host, IP literals included).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr045Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Cause: `UnusableDohUrlResolver` answered every host, IP literals and `localhost` included, with no address. Fix: it now answers an IP literal (bracketed or not) with itself and `localhost`/`*.localhost` with `::1`, `127.0.0.1`, as `DohDnsResolver` does without a query; any other name still gets none (exit 6). Doc comment in `EngineComposition.Dns.cs` corrected, and `EngineCompositionDohTests`' `UnusableDohUrlResolver_ResolvesEveryNameToNoAddress` renamed `..._ResolvesANameToNoAddress`, since it no longer covers every name (no other Doing task named either file). The decision is "match the platform's curl", so it is recorded here and needs no ADR.
- Measured real curl 8.22.0 (Schannel, `/ucrt64/bin/curl.exe`) and Curl with `--doh-url "a b"` and `--doh-url ftp://127.0.0.1:20002/` to `http://H:20002/` (closed port): `127.0.0.1`, `localhost` and `[::1]` give exit 7 `Failed to connect to H:20002 after N ms: Could not connect to server` in both; `example.invalid` gives exit 6 `Could not resolve host: example.invalid` in both (this variant already matched and still does).
- The repro commands, `curl -sS --doh-url "a b" http://127.0.0.1:21745/` and `curl -sS --doh-url ftp://127.0.0.1:20002/ http://127.0.0.1:21745/` (port 21745 because 20000 was in use), run through `Record-CurlExchange.ps1` against `HTTP/1.1 200 OK` / `Content-Length: 5` / `hello`: REF and PORT both exit 0, stdout `hello`, empty stderr, and the same request bytes (`GET / HTTP/1.1`, `Host: 127.0.0.1:21745`, `User-Agent: curl/8.22.0`, `Accept: */*`). Before the fix PORT gave exit 6.
- `Cgr045Tests` (8 cases) failed 7 of 8 against the old resolver and pass with the fix. The full fast run passed, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` shows 0 failing members. The tests use no paths or OS text, so they do not depend on the platform; Linux and macOS are left to CI.

## Log

- 2026-10-26: Created from CGR-045 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. An unusable --doh-url still connects to IP literals and localhost, as curl 8.22.0 does; other names fail with exit 6
