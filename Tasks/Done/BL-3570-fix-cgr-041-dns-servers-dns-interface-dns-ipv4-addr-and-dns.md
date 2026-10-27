---
id: BL-3570
title: Fix CGR-041: --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr are accepted and used; both non-c-ares reference builds refuse them w...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3545]
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineOption.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineGlobalState.cs, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Cli.UnitTests/Cgr041Tests.cs, Curl.Cli.UnitTests/CAresBuildParser.cs, Curl.Cli.UnitTests/CommandLineDnsOptionTests.cs, Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs, Curl.Cli.UnitTests/CommandLineNextGroupTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeProtocolOptionTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeOptionCoverageTests.cs, Curl.Console.UnitTests/CAresBuildParser.cs, Curl.Console.UnitTests/EasyOptionsFromCommandLineTests.cs, Curl.Engine.UnitTests/EngineCompositionDnsServersTests.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs, Curl.Engine.IntegrationTests/EngineCompositionDnsServersIntegrationTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3570 — Fix CGR-041: --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr are accepted and used; both non-c-ares reference builds refuse them w...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-041 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-041: `--dns-servers`, `--dns-interface`, `--dns-ipv4-addr` and `--dns-ipv6-addr` are accepted and used; both non-c-ares reference builds refuse them with exit 2

- **Severity:** Medium
- **Area:** cli-options, proxy-connect
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [proxy-connect PC-17] Probable. The difference reproduces, but it depends on the build: a c-ares curl accepts these options.
- **Related existing finding (GF/AF):** none
- **Source findings:** `cli-options CLIO-06`, `proxy-connect PC-17`

**Reproduction**

- *Setup (cli-options):* Evidence `SCR\evidence\cli-options`. Windows: start the echo server `pwsh -NoProfile -File SCR\evidence\cli-options\echoserver.ps1 -Port 20000` (replies 200 with the raw request bytes as the body); `@U` = `http://127.0.0.1:20000/`; port 20002 is left closed. Harness: `SCR\evidence\cli-options\h.sh <name> <args>` runs REF and PORT and stores exit code, stdout, stderr and files. Linux: `python3 linux/echo.py 20001` (in the evidence folder); `URL` = `http://127.0.0.1:20001/`. Verifier scripts: `verify/v1_win.sh`, `verify/v4_win.sh`, `verify/v2_linux.sh`, `verify/v3_linux.sh`.
- *Setup (proxy-connect):* EV = `SCR\evidence\proxy-connect`. Scripted recording server: `EV\srv\out\srv.exe <port> <log> '<script for conn 1>' ['<script for conn 2>' ...]` (Linux `EV/srv/outlinux/srv`); steps are `;`-separated: `H` read a request head, `HN` read a head and answer nothing yet, `OK`/`CE`/`403`/`403B`/`407B` canned replies, `W:<bytes>` write literal bytes, `S4`/`S5G`/`S5U`/`S5R` read SOCKS messages, `E` record raw bytes until EOF. In repros `srv` means this binary. Harnesses `EV\harness.ps1` and `EV\lharness.sh`; verifier `EV\v_win*.ps1`, `EV\v_linux.sh`, `EV\v_l3.sh`.
- **[cli-options CLIO-06]** --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr are accepted; REF says they are not supported
  - `curl --dns-servers 127.0.0.1 -s @U`; `curl --dns-interface lo -s @U`. Scripts: `verify/v1_win.sh`, `verify/v2_linux.sh`.
- **[proxy-connect PC-17]** --dns-ipv4-addr, --dns-ipv6-addr, --dns-interface and --dns-servers are accepted; both non-c-ares reference builds reject them with exit 2
  - run `srv 20600 log 'H;OK'`, then `curl -sS --dns-ipv4-addr 127.0.0.1 http://127.0.0.1:20600/x`. Verifier cases: v24 and VL19.

Commands (run each with REF and with PORT):

```sh
# [cli-options CLIO-06]
curl --dns-servers 127.0.0.1 -s @U
curl --dns-interface lo -s @U
# [proxy-connect PC-17]
srv 20600 log 'H;OK'
curl -sS --dns-ipv4-addr 127.0.0.1 http://127.0.0.1:20600/x
```

**Expected (real curl)**

- **[cli-options CLIO-06]** *REF (8.22 on Windows and 8.18 on Linux):* exit 2, `curl: option --dns-servers: the installed libcurl version does not support this`.
- **[proxy-connect PC-17]** exit 2, `curl: option --dns-ipv4-addr: the installed libcurl version does not support this`, then the "try curl --help" line.

**Actual (Curl)**

- **[cli-options CLIO-06]** exit 0 and the transfer runs. -V does not claim c-ares.
- **[proxy-connect PC-17]** exit 0 and the transfer runs.

**Suspected location in source**

- **[cli-options CLIO-06]** `CommandLineOptionTable.cs` (dns-* rows); `Curl.Networking.UnitLibrary/DnsServerList.cs`.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr041Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 6, first pass, no code edited): plan. ADR-0454 kept the four DNS options accepted on every platform; ADR-0540 later set the precedent of refusing `--http3` as every reference build does, behind a parser seam (`ActsAsQuicBuild`, `RefusedWithoutQuic`, test-only `QuicBuildParser`). Do the same for c-ares: a new ADR, superseding ADR-0454 point 2 for the DNS options (`--ech` stays as it is). Add `CommandLineGlobalState.ActsAsCAresBuild` plus a matching `CommandLineOptions.ActsAsCAresBuild` property, a `CommandLineOption.RefusedWithoutCAres()` row modifier built like `RefusedWithoutQuic` (refusal after "requires parameter" and before the value is checked), and a `CommandLineParser.Parse` overload that takes `actsAsCAresBuild` (the executable never sets it). Mark the four dns-* rows in `CommandLineOptionTable.cs`, and have `--ai-help` say they are refused on every platform (`CurlAiHelpText.cs`). Point the existing tests at a new test-only `CAresBuildParser` (Cli and Console test projects): CommandLineDnsOptionTests, the blank-argument test (blank values are now refused as unsupported), the next-group test, the LibcurlSourceCode tests and Console's EasyOptionsFromCommandLineTests. Pin the refusal (exit 2, `curl: option --dns-servers: the installed libcurl version does not support this` and the try-help line) in `Cgr041Tests.cs`. `Curl.Networking.UnitLibrary/DnsServerList.cs` needs no change, so it is dropped from touches.
- The fix needs `Curl.Cli.UnitLibrary/CommandLineOptions.cs`, which BL-3545 (in Doing on work/dark-factory) also names. Its private `globals` field means the refusal predicate cannot read the shared state without a property on that class, so BL-3545 goes in depends-on and the task goes back to Backlog. No other task in Doing names any of the other files.
- 2026-10-26 (lane 8): touches widened by the three Engine test files that parse the DNS options through the command line (EngineCompositionDnsServersTests, EngineCompositionDohTests, EngineCompositionDnsServersIntegrationTests); no task in Doing on work/dark-factory names them. They now parse as a c-ares build, so the resolver tests still reach the hand-built resolver. `Curl.Console.UnitTests/CAresBuildParser.cs` was not needed: EasyOptionsFromCommandLineTests already calls the full parser overload inline (as for QUIC and ECH), and the Engine tests do the same.
- Decision (ADR-0837, decided by Claude under Stewart's delegation): the four rows use `CommandLineOption.RefusedWithoutCAres()`, refused unless `CommandLineOptions.ActsAsCAresBuild`, which only `CommandLineParser.Parse(..., actsAsCAresBuild: true)` sets. This supersedes ADR-0454 decision 2 for these four options. `--ai-help` gives each section an every-platform refusal line. The refusal lines moved into the `CurlAiHelpText.ReferenceBuildRefusalLines` table to stay under the complexity gate. `Measure-CodeQuality -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Repros: real curl (`C:\Windows\System32\curl.exe`, 8.21.0 Schannel; the report gives the same answer for 8.22.0 Schannel and 8.18.0 OpenSSL) against Curl (`Curl.Console/bin/Debug/net10.0/curl.exe`), URL `http://127.0.0.1:20002/x`. Both refuse before connecting, so neither sends anything on the wire and no server is needed:
  - `--dns-servers 127.0.0.1 -s`: both exit 2, 0 stdout bytes, `curl: option --dns-servers: the installed libcurl version does not support this` plus the try-help line. Before the fix Curl exited 0 and ran the transfer.
  - `--dns-interface lo -s`, `-sS --dns-ipv4-addr 127.0.0.1`, `-sS --dns-ipv6-addr ::1`, `--dns-servers ''` and `--dns-servers` with the URL taken as its value: both exit 2, 0 stdout bytes, and the same line naming the option as typed.
  - Already matched and still matches: `--no-dns-servers x`, both exit 2 with `the given option cannot be reversed with a --no- prefix`.
  - Try-help line: the local 8.21.0 prints `curl: try 'curl --help' for more information` (built without the manual). Curl prints the `or 'curl --manual'` form (`CommandLineRefusal.TryHelpLine`), as the 8.22.0 reference does, for every refusal. That is outside this issue.
- ADR-0482: `Curl.Cli.UnitTests/Cgr041Tests.cs` pins the refusal for both the Windows and the non-Windows parse. Before the fix those parses were accepted, so its refusal assertions fail without the fix. CommandLineDnsOptionTests now parses through `CAresBuildParser`, and CommandLineBlankArgumentTests lists the four as refused by every reference build. The NextGroup and LibcurlSourceCodeOptionCoverage tests needed no change.
- Platform check: no path, OS error or certificate is involved, and Cgr041Tests runs both parses on every platform. The Linux and macOS CI criterion is met by keeping the change platform-neutral with the fast tests green here.
- `dotnet build` is clean. Fast tests: all 46 test projects passed (Cli 4399, Console 2931, Engine 2579).

## Log

- 2026-10-26: Created from CGR-041 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Needs Curl.Cli.UnitLibrary/CommandLineOptions.cs, held by BL-3545 (Doing); plan recorded under Notes
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr are refused with exit 2 as both reference builds refuse them
