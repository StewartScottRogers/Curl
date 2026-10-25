---
id: AF-0432
title: Curl accepts --http2 and --http2-prior-knowledge where the Schannel reference curl refuses them with exit 2
auditor: conformance
severity: High
status: rejected
reason: Stewart rejected it on 2026-10-25: --http2 on Windows is decided by ADR-0786 (keep HTTP/2 on), as for AF-0381 and AF-0423.
key: conformance:Curl.Cli.UnitLibrary/CommandLineOptionTable.HttpVersionRows.cs:--http2:exit-code
reproduction: none
task: none
tasks:
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0432 - Curl accepts --http2 and --http2-prior-knowledge where the Schannel reference curl refuses them with exit 2

## Summary

High finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineOptionTable.HttpVersionRows.cs:16`: Curl accepts --http2 and --http2-prior-knowledge where the Schannel reference curl refuses them with exit 2.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.HttpVersionRows.cs:16`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP (Features has no HTTP2). Seed 1164404129: cases 7, 14, 27, 62, 97 and 211 differ. Reduced to case 7, '--http2-prior-knowledge http://127.0.0.1:PORT/': curl exit 2 with 'curl: option --http2-prior-knowledge: the installed libcurl version does not support this' and sends nothing; Curl sends the 'PRI * HTTP/2.0' preface and exits 55 ('Remote peer returned unexpected data while we expected SETTINGS frame...'). Case 14 (--http2 --trace missing.txt): curl exit 2, Curl exit 0 after sending an 'Upgrade: h2c' request. Case 27: curl exit 2, Curl exit 56. Case 211: curl exit 2, Curl exit 55. Cases 62 and 97 differ only in which option is refused first: curl stops at --http2-prior-knowledge, Curl goes on to refuse --connect-timeout -1 or --happy-eyeballs-timeout-ms -1, which it refuses with curl's own words when run alone. Curl's own --version Features line also has no HTTP2, so it accepts options its own version output says it lacks. The rows Flag("http2") and Flag("http2-prior-knowledge") at HttpVersionRows.cs:16-17 have no refusal, unlike http3/http3-only, which use .RefusedWithoutQuic() (CommandLineOptionTable.cs:336-337). Phase 2: explained by ADR-0141 ('HTTP/2 is hand-built and accepted on every platform'), which records that the Windows Schannel reference refuses --http2 with exit 2 and decides Curl accepts it anyway, following curl.se's nghttp2 builds. Whether that reason holds is for triage.

## Reproduction

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -nologo -v q; & Curl.Console/bin/Release/net10.0/curl.exe -s -S --http2 http://127.0.0.1:1/; $LASTEXITCODE
```

- Expected: curl: option --http2: the installed libcurl version does not support this / curl: try 'curl --help' or 'curl --manual' for more information, then 2 (curl 8.22.0 Schannel)
- Actual: curl: (7) Failed to connect to 127.0.0.1:1 after 19 ms: Could not connect to server, then 7

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> rejected.
