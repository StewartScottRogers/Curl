---
id: BL-2050
title: Fix AF-0157: Curl.Core.UnitLibrary/CLAUDE.md says seven classes are not yet wired into Curl.Console and --ipfs-gateway is not yet parsed; all are wired and the option is parsed
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Core.UnitLibrary]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2050 — Fix AF-0157: Curl.Core.UnitLibrary/CLAUDE.md says seven classes are not yet wired into Curl.Console and --ipfs-gateway is not yet parsed; all are wired and the option is parsed

## Goal

The defect the audit office reported as AF-0157 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0157 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0157-curl-core-unitlibrary-claude-md-says-seven-classes.md`.

Location: `Curl.Core.UnitLibrary/CLAUDE.md:75`

Location: `Curl.Core.UnitLibrary/CLAUDE.md:75`

Curl.Core.UnitLibrary/CLAUDE.md says 'It is not yet wired into Curl.Console' 7 times: line 34 (ProtocolDispatcher), 75 (TransferRetrier, 'BL-241'), 82 (UrlSchemeGuesser), 91 (ProxySelector, 'BL-238'), 109 (MultipartFormBodyBuilder), 133 (IpfsGatewayRewriter, 'BL-240') and 156 (HstsCache, 'BL-621'). Line 134 adds '`--ipfs-gateway` is not yet parsed (BL-353)'. The code: Curl.Console/CurlComposition.cs:1317 and 1459 construct `new ProtocolDispatcher(...)`; CurlComposition.cs:1320 and 1462 construct `new ProxySelector(...)`; Curl.Console/CurlCommandRunner.cs:4702 constructs `TransferRetrier retrier = new(...)`; CurlCommandRunner.cs:1826 calls `UrlSchemeGuesser.AddScheme`; CurlCommandRunner.cs:255/402 take a `MultipartFormBodyBuilder`; CurlCommandRunner.cs:2361/2396 call `IpfsGatewayRewriter.IsIpfsUrl`; Curl.Console/HstsCacheFile.cs:59-60 and CurlCommandRunner.cs:2759/2796 read and write the HstsCache. Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:329 parses `ipfs-gateway`. An agent that trusts the document would plan or redo wiring work that already exists, or decide these features are not reachable from the command line. The retry paragraph also leaves out the FTP 4xx retry reason that TransferRetrier.cs:184-190 implements.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Core.UnitLibrary/CLAUDE.md -Pattern 'not yet wired').Count; (Select-String -Path Curl.Console/*.cs -Pattern 'new ProtocolDispatcher\(|new ProxySelector\(|UrlSchemeGuesser\.AddScheme|TransferRetrier retrier|IpfsGatewayRewriter\.IsIpfsUrl' | Select-Object -ExpandProperty Filename -Unique) -join ','
```

- Expected: 0, then the Curl.Console files that use these classes (the document no longer claims they are unwired)
- Actual: 7, then CurlCommandRunner.cs,CurlComposition.cs,TransferDispatch.cs: the document says 'not yet wired' 7 times while Curl.Console uses every class it names

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Replaced all seven "not yet wired" claims in `Curl.Core.UnitLibrary/CLAUDE.md` with where `Curl.Console` uses each class, dropped "`--ipfs-gateway` is not yet parsed", and added the FTP/FTPS 4xx retry reason (`: FTP error`). Reproduction now prints 0. Docs only; build clean, fast tests green.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. Curl.Core.UnitLibrary/CLAUDE.md no longer says classes are unwired; reproduction prints 0
