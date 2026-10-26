---
id: BL-3571
title: Fix CGR-042: --ech is accepted (values other than true/hard give exit 43 "setopt 0x2855 got bad argument"); REF refuses it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitTests/Cgr042Tests.cs, Curl.Cli.UnitLibrary/CommandLineOption.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Cli.UnitTests/EchBuildParser.cs, Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs, Curl.Cli.UnitTests/CommandLineTlsHandshakeOptionTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeProtocolOptionTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/CurlCommandRunnerEchModeTests.cs, Curl.Console.UnitTests/EasyOptionsFromCommandLineTests.cs, Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Conformance.Differential.UnitLibrary/Cases/CliHttp/CliHttpCases.json]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3571 — Fix CGR-042: --ech is accepted (values other than true/hard give exit 43 "setopt 0x2855 got bad argument"); REF refuses it

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-042 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-042: --ech is accepted (values other than true/hard give exit 43 "setopt 0x2855 got bad argument"); REF refuses it

- **Severity:** Medium
- **Area:** cli-options
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed
- **Related existing finding (GF/AF):** GF-0029, GF-0086, GF-0092
- **Source findings:** `cli-options CLIO-07`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- `curl --ech true -s @U`; `curl -sS --ech 0 -o NUL http://127.0.0.1:20002/`.

Commands (run each with REF and with PORT):

```sh
curl --ech true -s @U
curl -sS --ech 0 -o NUL http://127.0.0.1:20002/
```

**Expected (real curl)**

- exit 2, `curl: option --ech: the installed libcurl version does not support this`.

**Actual (Curl)**

- `--ech true` gives exit 0 and the transfer runs. `--ech 0` gives exit 43 `curl: (43) setopt 0x2855 got bad argument`.

**Suspected location in source**

- `Curl.Cli.UnitLibrary/CommandLineOptions.cs:1546`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr042Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Decision (ADR-0822, decided by Claude under Stewart's delegation): follow ADR-0540's `--http3` precedent. `--ech` is refused with `InstalledLibcurlDoesNotSupport` on every platform unless the parse reads as an ECH build (`CommandLineOptions.ActsAsEchBuild`, `CommandLineOption.RefusedWithoutEch()`); `CommandLineParser.Parse` gains an optional `actsAsEchBuild`, `CurlCommandRunner` and both `CurlComposition.CreateRunner` overloads gain `parsesAsEchBuild` (off by default; tests and the upstream harness turn it on), so the hand-built ECH stays reachable and covered. `--ai-help` says every platform refuses it. Supersedes ADR-0520 decision 2.
- Touches widened (no task in Doing on origin/work/dark-factory names any of these files): the refusal needs the option row, parser and global state; the switch needs the Console runner and composition; every test that parses `--ech` (Cli, Console, Engine TlsClientOptionsMappingTests, the conformance harness) now asks for the ECH build; the CLI/HTTP differential case `ech` loses its `NoEch` limitation since Curl and the reference now agree.
- Measured with Record-CurlExchange.ps1 (curl 8.22.0 Schannel, Windows) and with Curl's curl.exe after the fix:
  - `curl --ech true -s http://127.0.0.1:20713/` (server answering 200 hello): REF exit 2, stdout 0 bytes, nothing sent, stderr `curl: option --ech: the installed libcurl version does not support this` + `curl: try 'curl --help' or 'curl --manual' for more information`. PORT (port 20714): identical exit, stdout, stderr bytes (cmp equal), nothing sent.
  - `curl -sS --ech 0 -o NUL http://127.0.0.1:20002/`: REF exit 2, same two stderr lines, no stdout, no file. PORT: identical (cmp equal). Before the fix PORT gave exit 0 and exit 43 `setopt 0x2855 got bad argument`.
  - Linux reference (OpenSSL tarball build, no ECH per ADR-0520) refuses the same way via `feature_ech`; the refusal is platform-neutral, pinned in Cgr042Tests for isWindows true and false.
- Focused tests: `Curl.Cli.UnitTests/Cgr042Tests.cs` (refusal for both repros on both build kinds, blank value, `pn:`; acceptance on the ECH build; `--ai-help` line). They fail before the fix (`--ech` was accepted).
- Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Console: 0 failing members. Full fast run: every test project passed (one Cli failure from the first run, LibcurlSourceCodeProtocolOptionTests parsing `--ech` as the non-ECH build, fixed and rerun green). Platform check: no path, OS error or certificate involved; CI on Linux and macOS covered by keeping it platform-neutral.

## Log

- 2026-10-26: Created from CGR-042 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. --ech is refused with exit 2 on every platform as every reference build refuses it; the hand-built ECH stays behind ActsAsEchBuild (ADR-0822)
