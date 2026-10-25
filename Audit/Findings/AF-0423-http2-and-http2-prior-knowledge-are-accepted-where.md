---
id: AF-0423
title: --http2 and --http2-prior-knowledge are accepted where the reference Schannel curl refuses them with exit 2
auditor: conformance
severity: High
status: rejected
reason: Stewart rejected it on 2026-10-24: --http2 on Windows is decided by ADR-0786 (keep HTTP/2 on), as for AF-0381.
key: conformance:Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:--http2:exit-code
reproduction: none
task: none
tasks:
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0423 - --http2 and --http2-prior-knowledge are accepted where the reference Schannel curl refuses them with exit 2

## Summary

High finding from the conformance auditor at `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:333`: --http2 and --http2-prior-knowledge are accepted where the reference Schannel curl refuses them with exit 2.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:333`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP (no nghttp2). Differential run seed 221176388, count 300: cases 27, 91, 108, 114, 155 and 242, and partly 175. Reduced to `--http2 http://127.0.0.1:PORT/`: curl exits 2 with `curl: option --http2: the installed libcurl version does not support this` and the try-help line; Curl exits 0 and makes the transfer. With --http2-prior-knowledge, curl exits 2 the same way; Curl tries an h2 connection (case 27: exit 55, `Remote peer returned unexpected data while we expected SETTINGS frame...`). Because Curl accepts the HTTP/2 option, a later bad option is reported instead of curl's refusal (cases 108, 114, 155). The rows come from HttpVersionRows() at line 333. Phase 2, explained by ADR-0141 ("HTTP/2 is hand-built and accepted on every platform..."): this divergence from the Schannel build is deliberate and recorded. The severity stays as found; whether the ADR's reason is good enough is Stewart's call at triage.

## Reproduction

Run from the repository root:

```powershell
$o=Join-Path $env:TEMP 'af-h2'; & .\Record-CurlExchange.ps1 -Port 50913 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs @('--http2','http://127.0.0.1:50913/'); & .\Record-CurlExchange.ps1 -Port 50913 -Curl (Resolve-Path 'Curl.Console\bin\Release\net10.0\curl.exe') -OutDirectory "$o\candidate" -CurlArgs @('--http2','http://127.0.0.1:50913/'); Get-Content "$o\curl\exitcode.txt","$o\candidate\exitcode.txt"
```

- Expected: 2 and 2, each with `curl: option --http2: the installed libcurl version does not support this` (the reference curl's result).
- Actual: curl 2; Curl 0, with the transfer made over HTTP/1.1.

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> rejected.
