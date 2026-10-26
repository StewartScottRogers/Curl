---
id: BL-3581
title: Fix CGR-052: Windows: percent-decoded UTF-8 userinfo is re-encoded to the ANSI code page in Basic auth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/BasicAndBearerAuthenticator.cs, Curl.Authentication.UnitTests/Cgr052Tests.cs, Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitTests/Cgr052Tests.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr052Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3581 — Fix CGR-052: Windows: percent-decoded UTF-8 userinfo is re-encoded to the ANSI code page in Basic auth

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-052 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-052: Windows: percent-decoded UTF-8 userinfo is re-encoded to the ANSI code page in Basic auth

- **Severity:** Medium (Corrected from High, because non-ASCII credentials inside the URL are uncommon. `-u` with non-ASCII matches REF)
- **Area:** url-parsing
- **Platform(s):** Windows
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** none
- **Source findings:** `url-parsing UP-03`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- `curl -sS "http://%C3%A9:%E2%82%AC@127.0.0.1:20200/a"` (v03_user_pct_utf8)
- The same URL written as raw UTF-8 in a `-K` config file (v03b_cfg_user_utf8).

Commands (run each with REF and with PORT):

```sh
curl -sS "http://%C3%A9:%E2%82%AC@127.0.0.1:20200/a"
```

**Expected (real curl)**

- `Authorization: Basic w6k64oKs`, which is the bytes C3 A9 3A E2 82 AC.

**Actual (Curl)**

- `Authorization: Basic 6TqA`, which is the bytes E9 3A 80 (CP1252).

**Notes**

- *Linux:* PORT-LINUX matches REF (lv03_user_utf8 SAME).

**Suspected location in source**

- `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:347` (`Uri.UnescapeDataString`) and `Curl.Authentication.UnitLibrary/BasicAndBearerAuthenticator.cs:43` (`CredentialEncoding.ForPlatform`).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr052Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Plan: decode the URL's user information to bytes and read them back through the credential encoding the authenticators use (ANSI code page on Windows, UTF-8 elsewhere), and read the netrc file through it too, so the authenticator's encode gives back the bytes curl sends; respell the user information of a url read from a UTF-8 -K file as headers already are (AsWireText). Decided by Claude under Stewart's delegation: ADR-0821.
- Measured 2026-10-26 on Windows with Record-CurlExchange.ps1, curl 8.22.0 Schannel (REF) against Curl's built curl.exe (PORT), loopback port 20581, 200 with empty body; request.bin, stdout.bin, stderr.txt and exitcode.txt compared by hash, all equal for every case after the fix:
  1. curl -sS "http://%C3%A9:%E2%82%AC@127.0.0.1:20581/a": REF and PORT exit 0, no output, Authorization: Basic w6k64oKs (PORT before: Basic 6TqA).
  2. curl -sS -K cfg.txt with UTF-8 url = "http://é:€@127.0.0.1:20581/a" (v03b): both Basic w6k64oKs (PORT before: Basic 6TqA).
  3. curl -sS "http://é@127.0.0.1:20581/a" (raw on the command line, already matched): both Basic 6To= (E9 3A).
  4. --netrc-file with UTF-8 machine 127.0.0.1 login é password €: both Basic w6k64oKs.
  5. the same netrc with http://%C3%A9@127.0.0.1:20581/a: both Basic w6k64oKs (the URL user still matches the entry).
- No change was needed in BasicAndBearerAuthenticator.cs, so Curl.Authentication.UnitTests/Cgr052Tests.cs was not created: the fix is in the lookup (Engine) and the config-file URL (Cli), each with its own Cgr052Tests.cs. Cgr052Tests.TryLookUp_PercentEncodedUtf8OnWindows_SendsTheDecodedBytes gives E9 3A 80 with the old Uri.UnescapeDataString decode and C3 A9 3A E2 82 AC now; Parse_ConfigFileUrlWithUtf8UserInformationOnWindows_... likewise fails without the respell (measured as case 2 before the Cli change).
- Touches widened to Curl.Cli.UnitLibrary/CommandLineOptionTable.cs and Curl.Cli.UnitTests/Cgr052Tests.cs for the -K repro; no task in Doing on origin/work/dark-factory names them. Curl.Console.UnitTests/CurlCommandRunnerCredentialControlCodeTests.cs's ftp user %c3%a9 row pinned the old string and now checks the bytes the platform encoding gives back (one-line fix, kept here since this change caused it).
- Platform check: the tests use CredentialEncoding.ForPlatform(true) (Windows-1252 off Windows) or UTF-8 explicitly and no path, so they pass the same on Linux and macOS; Linux behaviour for valid UTF-8 is unchanged (REF-LINUX already matched).
- Follow-up filed: BL-3849 (FTP writes USER/PASS in Latin-1, not the platform credential bytes).
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Cli.UnitLibrary: 0 failing members. Fast tests: every project passed (Curl.Console.UnitTests 2889 passed after its test update, Curl.Engine.UnitTests 2518).
## Log

- 2026-10-26: Created from CGR-052 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. URL percent-decoded and netrc credentials, and -K file URL userinfo, go on the wire as curl's bytes on Windows (CGR-052)
