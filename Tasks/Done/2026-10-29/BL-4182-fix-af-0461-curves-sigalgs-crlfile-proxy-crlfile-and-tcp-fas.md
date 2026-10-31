---
id: BL-4182
title: Fix AF-0461: --curves, --sigalgs, --crlfile, --proxy-crlfile and --tcp-fastopen fail with exit 4 'setopt 0x... got bad argument' on Windows where curl 8.22.0 Schannel carries on and exits 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests/NotBuiltInTlsSetoptWarningTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4182 — Fix AF-0461: --curves, --sigalgs, --crlfile, --proxy-crlfile and --tcp-fastopen fail with exit 4 'setopt 0x... got bad argument' on Windows where curl 8.22.0 Schannel carries on and exits 0

## Goal

The defect the audit office reported as AF-0461 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0461 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0461-curves-sigalgs-crlfile-proxy-crlfile-and-tcp-fasto.md`.

Location: `Curl.Console/ToolEasyHandle.cs:20`

Location: `Curl.Console/ToolEasyHandle.cs:20`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP (debug-enabled libcurl). Invoke-DifferentialConformance.ps1 -Count 300 -Seed 1311569991: cases 14, 46, 195 (--sigalgs, setopt 0x2858 CURLOPT_SSL_SIGNATURE_ALGORITHMS), 44, 141, 241 (--curves, 0x283a CURLOPT_SSL_EC_CURVES), 63, 71 (--crlfile, 0x27b9 CURLOPT_CRLFILE) and 27, 83, 167, 268 (--tcp-fastopen, 0xf4 CURLOPT_TCP_FASTOPEN) differ in exit code, stderr and request. Reduced to one option each against http://127.0.0.1:50363/: curl exits 0, sends the 79-byte GET and prints nothing; Curl exits 4, sends nothing and prints e.g. 'curl: (4) setopt 0x283a got bad argument'. A probe found the same for --proxy-crlfile (curl 0; Curl 4, 'setopt 0x2814 got bad argument'). (For --crlfile the file must exist, otherwise both exit 2 alike.) Case 46 (--interface - --sigalgs @f.txt): curl reaches the connect and fails with exit 45, while Curl stops before connecting with exit 4. Cause: Libcurl answers CURLE_NOT_BUILT_IN for these setopts when OperatingSystem.IsWindows() (Libcurl/Options/Tls/EasyTlsOptionSetters.cs:35,41,44 RevocationListFileIsBuiltIn/EllipticCurvesIsBuiltIn/SignatureAlgorithmsIsBuiltIn; Libcurl/Options/Network/EasyNetworkOptionSetters.cs:43 TcpFastOpenIsBuiltIn). ToolEasyHandle.SetOptions then fails the transfer for any NOT_BUILT_IN answer except the four options in IgnoredWhenNotBuiltIn (Capath, ProxyCapath, Tls13Ciphers, ProxyTls13Ciphers; line 20), whereas the reference curl tool carries on past these five silently. Scripts that pass any of these options get exit 4 instead of a completed transfer. Phase 2: explained in part by ADR-0995 (Libcurl answers the Schannel build's NOT_BUILT_IN for CRLFILE, PROXY_CRLFILE, SSL_EC_CURVES, SSL_SIGNATURE_ALGORITHMS and TCP_FASTOPEN) and ADR-0975 (the tool carries on past NOT_BUILT_IN only for the CA path and TLS 1.3 cipher setopts, and fails for any other). Neither ADR measured or decided the curl tool's exit for these five options, so the difference is undocumented and the finding stands.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q | Out-Null; foreach ($c in @('C:\Program Files\Git\ucrt64\bin\curl.exe', (Resolve-Path Curl.Console\bin\Release\net10.0\curl.exe).Path)) { foreach ($o in @(@('--curves','1'), @('--sigalgs','1'), @('--tcp-fastopen'))) { $d = Join-Path $env:TEMP ([guid]::NewGuid()); New-Item -ItemType Directory -Force $d | Out-Null; & .\Record-CurlExchange.ps1 -Port 50363 -Curl $c -OutDirectory $d -CurlArgs ($o + 'http://127.0.0.1:50363/') *> $null; '{0} {1}: exit {2} {3}' -f $c, ($o -join ' '), (Get-Content $d\exitcode.txt), (Get-Content $d\stderr.txt | Select-String 'curl:') } }
```

- Expected: Every run exits 0 with no 'curl:' error line, the reference curl and Curl alike (curl 8.22.0 Schannel carries on and sends the GET).
- Actual: Reference curl: exit 0 for all three. Curl.Console: '--curves 1: exit 4 curl: (4) setopt 0x283a got bad argument', '--sigalgs 1: exit 4 curl: (4) setopt 0x2858 got bad argument', '--tcp-fastopen: exit 4 curl: (4) setopt 0xf4 got bad argument'; no request sent.

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: `ToolEasyHandle.IgnoredWhenNotBuiltIn` now also holds `CURLOPT_CRLFILE`, `CURLOPT_PROXY_CRLFILE`, `CURLOPT_SSL_EC_CURVES`, `CURLOPT_SSL_SIGNATURE_ALGORITHMS` and `CURLOPT_TCP_FASTOPEN`, so a `CURLE_NOT_BUILT_IN` answer to them (Libcurl on Windows, ADR-0995) is passed over silently, as curl 8.22.0 Schannel does. Decision recorded in ADR-1036.
- Touches: added `Curl.Console.UnitTests/NotBuiltInTlsSetoptWarningTests.cs`, the existing test class for this list (five new DataRows); no task in Doing on origin/work/dark-factory names it.
- Measured: the finding's reproduction, extended to `--crlfile global.json` and `--proxy-crlfile global.json`, gives exit 0, no `curl:` or `Warning` line and 472 bytes of stderr (progress meter only) for reference curl 8.22.0 and Curl alike.
- Build clean; fast tests green (0 failures); Measure-CodeQuality.ps1 -Library Curl.Console: 0 failing members.
- CI on Linux and macOS: the change is platform-neutral (a set membership, tested with a fake setopt), so nothing differs there.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-29: Doing -> Done. --crlfile, --proxy-crlfile, --curves, --sigalgs and --tcp-fastopen carry on and exit 0 on Windows as curl 8.22.0 Schannel does
