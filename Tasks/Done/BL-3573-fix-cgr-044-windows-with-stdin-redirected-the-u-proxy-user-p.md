---
id: BL-3573
title: Fix CGR-044: Windows: with stdin redirected, the -u/--proxy-user password prompt returns an empty password at once; REF waits for console input
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3854]
touches: [Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs, Curl.Cli.UnitLibrary/WindowsConsolePasswordInput.cs, Curl.Cli.UnitTests/Cgr044Tests.cs, Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3573 — Fix CGR-044: Windows: with stdin redirected, the -u/--proxy-user password prompt returns an empty password at once; REF waits for console input

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-044 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-044: Windows: with stdin redirected, the -u/--proxy-user password prompt returns an empty password at once; REF waits for console input

- **Severity:** Medium
- **Area:** cli-options
- **Platform(s):** Windows
- **Confidence:** Confirmed. Detail: Confirmed (in the redirected harness; an interactive console was not tested)
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-09`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- `curl -s -u user @U < pw.txt` (pw.txt is `secret\n`) and `... < /dev/null`, with an 8 s timeout. Scripts: `repro/r10_password_win.sh`, `verify/v1_win.sh`.

Commands (run each with REF and with PORT):

```sh
curl -s -u user @U < pw.txt
```

**Expected (real curl)**

- prints `Enter host password for user 'user':` and blocks reading the console (not stdin). It is killed at the timeout (exit 124).

**Actual (Curl)**

- prints the prompt and a newline, then sends `Authorization: Basic dXNlcjo=` (`user:`, an empty password) with exit 0.

**Suspected location in source**

- `Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs:57` (`Console.ReadKey`) and the `InvalidOperationException` catch in `ReadKeysUntilEnter` (about line 76).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr044Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured REF (curl 8.21.0 Schannel, `C:\Windows\System32\curl.exe`; 8.22.0 not installed here): `timeout 8 curl -s -u user http://127.0.0.1:20002/ < pw.txt` (pw.txt `secret\n`) and `... < /dev/null` both print `Enter host password for user 'user':` (no LF) to stderr, nothing to stdout, and are killed at the timeout (exit 124); nothing is sent, so the closed port 20002 stands in for the echo server.
- Fix (in this run's stash): new `WindowsConsolePasswordInput` reads `CONIN$` as `_getch` does (`CreateFileW`, `GetConsoleMode`/`SetConsoleMode(0)`, `ReadConsoleW` one character, modes restored), `ConsolePasswordPrompt.ForPlatform(true)` uses it, and a public `KeyReader` lets `Cgr044Tests` check the wiring; ADR-0819 records the decision. Added `WindowsConsolePasswordInput.cs` to touches (a new file no other task names). With it, PORT via `dotnet run --project Curl.Console --no-build -- -s -u user http://127.0.0.1:20002/` gives the same as REF for both inputs (exit 124, same stderr, empty stdout). Build clean.
- Requeued for BL-3854: with the fix, `dotnet test Curl.Cli.UnitTests` ran past 60 minutes, because tests parse a password-less `-u` through `ConsolePasswordPrompt.ForProcessConsole` and now wait on the lane's console. BL-3854 gives those tests a fake prompt. `Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs` may also need checking. Still to do after BL-3854: fast tests, Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary, commit.

- Finished after BL-3854: removed `ConsolePasswordPromptTests.ForProcessConsole_InputRedirected_ReturnsAnEmptyPassword`, which pinned the old empty password and would now wait on the console (added that file to touches; no other Doing task names it). `Curl.Cli.UnitTests` 4372 passed, no hang; Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members; full fast tests green; build clean. Cgr044Tests fails before the fix (KeyReader was the Console.ReadKey lambda) and passes after. The change is Windows-only behind ForPlatform(isWindows) and the tests compare delegates, so they are platform-neutral.

## Log

- 2026-10-26: Created from CGR-044 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3854: with the CONIN$ prompt fix, fast tests that parse a password-less -u through ConsolePasswordPrompt.ForProcessConsole wait on the console
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Windows -u/--proxy-user prompt reads CONIN$ like _getch; redirected stdin no longer yields an empty password
