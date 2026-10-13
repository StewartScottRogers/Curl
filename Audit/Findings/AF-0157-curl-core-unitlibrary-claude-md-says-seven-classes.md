---
id: AF-0157
title: Curl.Core.UnitLibrary/CLAUDE.md says seven classes are not yet wired into Curl.Console and --ipfs-gateway is not yet parsed; all are wired and the option is parsed
auditor: truthfulness
severity: High
status: accepted
reason: 
key: truthfulness:Curl.Core.UnitLibrary/CLAUDE.md:not-yet-wired-into-Curl.Console:false-statement
reproduction: none
task: BL-2050
tasks: BL-2050
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0157 - Curl.Core.UnitLibrary/CLAUDE.md says seven classes are not yet wired into Curl.Console and --ipfs-gateway is not yet parsed; all are wired and the option is parsed

## Summary

High finding from the truthfulness auditor at `Curl.Core.UnitLibrary/CLAUDE.md:75`: Curl.Core.UnitLibrary/CLAUDE.md says seven classes are not yet wired into Curl.Console and --ipfs-gateway is not yet parsed; all are wired and the option is parsed. Reported by an auditor flagged unreliable in 2026-10-11_0051.md.

## Evidence

Location: `Curl.Core.UnitLibrary/CLAUDE.md:75`

Curl.Core.UnitLibrary/CLAUDE.md says 'It is not yet wired into Curl.Console' 7 times: line 34 (ProtocolDispatcher), 75 (TransferRetrier, 'BL-241'), 82 (UrlSchemeGuesser), 91 (ProxySelector, 'BL-238'), 109 (MultipartFormBodyBuilder), 133 (IpfsGatewayRewriter, 'BL-240') and 156 (HstsCache, 'BL-621'). Line 134 adds '`--ipfs-gateway` is not yet parsed (BL-353)'. The code: Curl.Console/CurlComposition.cs:1317 and 1459 construct `new ProtocolDispatcher(...)`; CurlComposition.cs:1320 and 1462 construct `new ProxySelector(...)`; Curl.Console/CurlCommandRunner.cs:4702 constructs `TransferRetrier retrier = new(...)`; CurlCommandRunner.cs:1826 calls `UrlSchemeGuesser.AddScheme`; CurlCommandRunner.cs:255/402 take a `MultipartFormBodyBuilder`; CurlCommandRunner.cs:2361/2396 call `IpfsGatewayRewriter.IsIpfsUrl`; Curl.Console/HstsCacheFile.cs:59-60 and CurlCommandRunner.cs:2759/2796 read and write the HstsCache. Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:329 parses `ipfs-gateway`. An agent that trusts the document would plan or redo wiring work that already exists, or decide these features are not reachable from the command line. The retry paragraph also leaves out the FTP 4xx retry reason that TransferRetrier.cs:184-190 implements.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Core.UnitLibrary/CLAUDE.md -Pattern 'not yet wired').Count; (Select-String -Path Curl.Console/*.cs -Pattern 'new ProtocolDispatcher\(|new ProxySelector\(|UrlSchemeGuesser\.AddScheme|TransferRetrier retrier|IpfsGatewayRewriter\.IsIpfsUrl' | Select-Object -ExpandProperty Filename -Unique) -join ','
```

- Expected: 0, then the Curl.Console files that use these classes (the document no longer claims they are unwired)
- Actual: 7, then CurlCommandRunner.cs,CurlComposition.cs,TransferDispatch.cs: the document says 'not yet wired' 7 times while Curl.Console uses every class it names

## Re-audits

- 2026-10-11 | 2026-10-11_1638.md | not re-audited | overlaps planted defect PD-201 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: (Select-String Curl.Core.UnitLibrary/CLAUDE.md 'not yet wired').Count is 0. The wiring patterns are found in CurlCommandRunner.cs, CurlComposition.cs and TransferDispatch.cs, and the CLAUDE.md now says Curl.Console constructs or calls each class and that --ipfs-gateway is parsed by Curl.Cli.UnitLibrary's option table.
- 2026-10-12 | 2026-10-12_1653.md | not re-audited | overlaps planted defect PD-201 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction. (Select-String -Path Curl.Core.UnitLibrary/CLAUDE.md -Pattern 'not yet wired').Count is 0, and the wiring patterns match in CurlCommandRunner.cs, CurlComposition.cs and TransferDispatch.cs. Curl.Core.UnitLibrary/CLAUDE.md:131-132 now says 'Curl.Console uses it for ipfs:// and ipns:// URLs, and --ipfs-gateway is parsed by Curl.Cli.UnitLibrary's option table'.
- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the reproduction: (Select-String Curl.Core.UnitLibrary/CLAUDE.md 'not yet wired').Count = 0, and the wiring files are CurlCommandRunner.cs,CurlComposition.cs,TransferDispatch.cs. Curl.Core.UnitLibrary/CLAUDE.md lines 131-132 now say Curl.Console uses IpfsGatewayRewriter and that --ipfs-gateway is parsed by Curl.Cli.UnitLibrary's option table.

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
