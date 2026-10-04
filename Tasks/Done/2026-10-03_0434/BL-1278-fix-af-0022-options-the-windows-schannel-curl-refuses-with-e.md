---
id: BL-1278
title: Fix AF-0022: Options the Windows Schannel curl refuses with exit 2 (--http2, --http2-prior-knowledge, --http3, --http3-only, --tlsuser, --tlspassword, --tlsauthtype, --ssl-sessions) are accepted by Curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1278 — Fix AF-0022: Options the Windows Schannel curl refuses with exit 2 (--http2, --http2-prior-knowledge, --http3, --http3-only, --tlsuser, --tlspassword, --tlsauthtype, --ssl-sessions) are accepted by Curl

## Goal

The defect the audit office reported as AF-0022 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0022 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0022-options-the-windows-schannel-curl-refuses-with-exi.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. The inbox C:\Windows\System32\curl.exe (curl 8.21.0 (Windows) Schannel, features without HTTP2/HTTP3/TLS-SRP) also lacks them. Differential seed 863948043 cases 14, 21, 54, 57, 58, 71, 73, 85, 97, 99, 118, 124, 139, 155, 167, 208, 220, 240, 241, 243, 249, 263, 273, 276, 288 (25 cases). For each, curl stops at 'curl: option --X: the installed libcurl version does not support this' plus the try line, exit 2, no request. Curl runs the transfer instead: exit 0, or 16/56 for HTTP/2 prior knowledge against an HTTP/1 server, or 3 'HTTP/3 requested for non-HTTPS URL'. Or it fails at a later option's check: --tlsauthtype '' gives 'blank argument where content is expected' (cases 58, 220, 241); later options give their own errors (85, 97, 167, 240). Reduced: -s --tlsuser 1 URL, -s --http2 URL, -s --ssl-sessions f.txt URL: curl exit 2, Curl exit 0. Phase 2: explained by ADR-0141 (HTTP/2 hand-built and accepted on every platform), ADR-0144 (HTTP/3 hand-built), ADR-0151 (the ten TLS options accepted everywhere, including --ssl-sessions) and ADR-0328 (--tlsuser runs TLS-SRP as curl's OpenSSL build). These are recorded decisions to diverge from the platform build's refusal. Whether that is acceptable for a drop-in replacement is Stewart's call at triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q | Out-Null; foreach ($x in @(@('curl', "$env:ProgramFiles\Git\mingw64\bin\curl.exe"), @('candidate', '.\Curl.Console\bin\Release\net10.0\curl.exe'))) { foreach ($o in @('--http2'), @('--tlsuser', '1'), @('--ssl-sessions', 'f.txt')) { $d = "$env:TEMP\feat\$($x[0])"; & .\Record-CurlExchange.ps1 -Port 48295 -Curl $x[1] -OutDirectory $d -CurlArgs (@('-s') + $o + 'http://127.0.0.1:48295/') *> $null; '{0} {1}: exit {2}' -f $x[0], ($o -join ' '), (Get-Content "$d\exitcode.txt") } }
```

- Expected: candidate as curl: --http2 exit 2, --tlsuser 1 exit 2, --ssl-sessions f.txt exit 2, each with 'the installed libcurl version does not support this'
- Actual: curl: exit 2 for all three. candidate: exit 0 for all three, and the GET is sent

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Decision: ADR-0397 (decided by Claude under Stewart's delegation). On Windows the parser reads as the
  Schannel build and refuses `--http2`, `--http2-prior-knowledge`, `--http3`, `--http3-only`, `--tlsuser`,
  `--tlspassword`, `--tlsauthtype`, `--proxy-tlsuser`, `--proxy-tlspassword`, `--proxy-tlsauthtype` and
  `--ssl-sessions` with `the installed libcurl version does not support this`, exit 2; off Windows nothing
  changes. The three `--proxy-` TLS-SRP options were not in the finding but the same curl refuses them the
  same way, so they are included.
- Measured with `C:\Program Files\Git\mingw64\bin\curl.exe` (8.21.0 Schannel) on 2026-10-02: each option
  (also `--http2=x`, and each value option with an empty value) refused as above, even under `-s`; a value
  option as the last argument is `requires parameter` first; `--no-http2` etc. stay "cannot be reversed";
  in a `-K` file the line is `k.txt:1 config file option 'tlsuser' the installed libcurl version does not
  support this`, wrapped, then `option -K: ...`.
- Implementation: `CommandLineOption.RefusedBySchannelBuild()` wraps a row's applier; it checks
  `CommandLineOptions.ActsAsWindowsSchannelBuild`, set from the parser's `isWindows`. New overload
  `CommandLineParser.Parse(..., DefaultConfigFileSearch, bool isWindows)`; `CurlCommandRunner` and
  `CurlComposition.CreateRunner` take an optional `parsesAsWindowsBuild` (default: this process's platform).
  `--ai-help` gives each of the eleven sections a line saying Windows refuses it.
- touches widened (rule 3): the change made 37 Cli and 53 Console tests fail on Windows because they
  parsed `--http2`/`--http3`/TLS-SRP/`--ssl-sessions` as this platform. Those tests now parse as the OpenSSL
  build (`OpenSslBuildParser` helpers, `parsesAsWindowsBuild: false`), which needed `Curl.Cli.UnitTests`,
  `Curl.Console` and `Curl.Console.UnitTests`. No task in Doing on `origin/work/dark-factory` touched them.
- Reproduction after the fix: curl and candidate both exit 2 with the same two stderr lines for
  `--http2`, `--tlsuser 1` and `--ssl-sessions f.txt`.
- Tests: Curl.Cli.UnitTests 3752 passed (new `CommandLineSchannelBuildRefusalTests`), Curl.Console.UnitTests
  2437 passed (new `CurlCommandRunnerSchannelBuildRefusalTests`); all 33 test projects green.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. On Windows the eleven options curl's Schannel build lacks are refused with exit 2 as it refuses them (AF-0022, ADR-0397)
