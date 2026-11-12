---
id: AF-0703
title: --path-as-is is refused as unknown (exit 2) where curl accepts it and sends the request
auditor: conformance
severity: High
status: accepted
reason: Rejected by Stewart (2026-11-12: "reject AF-0703"): not a defect in Curl. It is audit 20261111-200237's planted defect (the seeder removed the path-as-is NegatableFlag row from CommandLineOptionTable); the real tree registers --path-as-is at Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:332.
key: conformance:--path-as-is:--path-as-is:refused-option
reproduction: none
task: BL-5006
tasks: BL-5006
found: 2026-11-11
found-at: df2273e7663bedd3179f7d3166167d9ce99c6ca0
scorecard: 2026-11-11_2002.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0703 - --path-as-is is refused as unknown (exit 2) where curl accepts it and sends the request

## Summary

High finding from the conformance auditor at `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs:174`: --path-as-is is refused as unknown (exit 2) where curl accepts it and sends the request. Reported by an auditor flagged unreliable in 2026-11-11_2002.md.

## Evidence

Location: `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs:174`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel. Seed 79116. Cases 21 and 25. curl exits 0 and sends GET / HTTP/1.1. Curl exits 2 with 'curl: option --path-as-is: is unknown', and nothing goes on the wire. The option is in CurlHelpTable.cs:165 and CurlOptionAliasTable.cs:174, and CommandLineOptions.cs:2006 documents it. The only grep hit for a quoted "path-as-is" outside doc comments in Curl.Cli.UnitLibrary is the alias table, so no parser case appears to handle it. The reduced command line is `--path-as-is http://127.0.0.1:PORT/` (case 21).

## Reproduction

Run from the repository root:

```powershell
& ./Record-CurlExchange.ps1 -Port 51490 -Curl ./Curl.Console/bin/Release/net10.0/curl.exe -OutDirectory $env:TEMP\c -CurlArgs @('--path-as-is','http://127.0.0.1:51490/'); Get-Content $env:TEMP\c\exitcode.txt
```

- Expected: Exit 0; GET / HTTP/1.1 sent.
- Actual: Exit 2; 'curl: option --path-as-is: is unknown'; no request.

## Re-audits

## Log

- 2026-11-12: rejected (Stewart). Rejected by Stewart (2026-11-12: "reject AF-0703"): not a defect in Curl. It is audit 20261111-200237's planted defect (the seeder removed the path-as-is NegatableFlag row from CommandLineOptionTable); the real tree registers --path-as-is at Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:332.
- 2026-11-12: deferred (Stewart), to reject it.
- 2026-11-11: filed proposed.
- 2026-11-12: proposed -> accepted.
