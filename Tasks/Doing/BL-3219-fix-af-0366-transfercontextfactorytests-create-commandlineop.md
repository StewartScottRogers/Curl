---
id: BL-3219
title: Fix AF-0366: TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine fails on the unmutated tree: the parser refuses its command line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3219 — Fix AF-0366: TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine fails on the unmutated tree: the parser refuses its command line

## Goal

The defect the audit office reported as AF-0366 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0366 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0366-transfercontextfactorytests-create-commandlineopti.md`.

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`

The Engine mutation baseline listed this test in excludedTests. Run alone it fails at the test's Parse helper (TransferContextFactoryTests.cs:942, Assert.IsTrue(parsed.IsAccepted), actual false) for the command line: --max-filesize 100 -d a=b -u user:secret -t TTYPE=vt100 --tftp-blksize 1024 --tftp-no-options --disable-epsv --no-ftp-skip-pasv-ip --ftp-method singlecwd --ftp-create-dirs --ftp-account billing --ftp-alternative-to-user "SITE AUTH" --ftp-pret -l -B -a --crlf -Q NOOP -Q -DELE x --create-file-mode 0600 --connect-timeout 3 --path-as-is -R -m 9 -z "Sun, 06 Nov 1994 08:49:37 GMT" tftp://example.com/x. Curl.Cli's CommandLineParser now refuses an option in this list that it is expected to accept. I did not isolate which option; the refusal is in Curl.Cli.UnitLibrary.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests --filter "FullyQualifiedName~TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine"
```

- Expected: Passed: 1, Failed: 0
- Actual: Failed: 1 - Assert.IsTrue(parsed.IsAccepted) at TransferContextFactoryTests.cs:942, actual false

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
