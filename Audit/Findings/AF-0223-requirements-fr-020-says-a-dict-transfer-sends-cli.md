---
id: AF-0223
title: Requirements FR-020 says a dict:// transfer sends CLIENT libcurl 8.21.0; the code sends CLIENT libcurl 8.22.0
auditor: truthfulness
severity: High
status: accepted
reason: 
key: truthfulness:Documentation/Product/Requirements.md:FR-020:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-15
found-at: 3e1062e68644127a82ba2f5ef33ceb1044cc6f67
scorecard: 2026-10-15_0156.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0223 - Requirements FR-020 says a dict:// transfer sends CLIENT libcurl 8.21.0; the code sends CLIENT libcurl 8.22.0

## Summary

High finding from the truthfulness auditor at `Documentation/Product/Requirements.md:66`: Requirements FR-020 says a dict:// transfer sends CLIENT libcurl 8.21.0; the code sends CLIENT libcurl 8.22.0. Reported by an auditor flagged unreliable in 2026-10-15_0156.md.

## Evidence

Location: `Documentation/Product/Requirements.md:66`

FR-020 reads: 'A dict:// transfer sends its whole request without waiting for the server's greeting: `CLIENT libcurl 8.21.0\r\n`, then one command line, then `QUIT\r\n`. Built; every command test in DictProtocolHandlerTests asserts the three lines'. The code sends something else: Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:37 is `public const string ClientLine = "CLIENT libcurl 8.22.0";`, and Curl.Protocol.Dict.UnitTests/DictProtocolHandlerTests.cs:16 pins `CLIENT libcurl 8.22.0\r\n`. An agent taking the requirement as the spec would pin or 'fix' the wrong bytes. The project's own CLAUDE.md already says the CLIENT line moved to 8.22.0 (ADR-0479, BL-2127).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Product/Requirements.md -Pattern 'CLIENT libcurl 8\.21\.0' | ForEach-Object { $_.LineNumber }; (Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'ClientLine = ').Line.Trim()
```

- Expected: No match in Requirements.md (FR-020 names the CLIENT line DictRequest.ClientLine holds), then: public const string ClientLine = "CLIENT libcurl 8.22.0";
- Actual: 66, then: public const string ClientLine = "CLIENT libcurl 8.22.0";

## Re-audits

## Log

- 2026-10-15: filed proposed.
- 2026-10-15: proposed -> accepted.
