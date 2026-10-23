---
id: AF-0366
title: TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine fails on the unmutated tree: the parser refuses its command line
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Engine.UnitTests/TransferContextFactoryTests.cs:Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine:failing-test
reproduction: none
task: BL-3219
tasks: BL-3219
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0366 - TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine fails on the unmutated tree: the parser refuses its command line

## Summary

High finding from the quality auditor at `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`: TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine fails on the unmutated tree: the parser refuses its command line. Reported by an auditor flagged unreliable in 2026-10-22_1902.md.

## Evidence

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`

The Engine mutation baseline listed this test in excludedTests. Run alone it fails at the test's Parse helper (TransferContextFactoryTests.cs:942, Assert.IsTrue(parsed.IsAccepted), actual false) for the command line: --max-filesize 100 -d a=b -u user:secret -t TTYPE=vt100 --tftp-blksize 1024 --tftp-no-options --disable-epsv --no-ftp-skip-pasv-ip --ftp-method singlecwd --ftp-create-dirs --ftp-account billing --ftp-alternative-to-user "SITE AUTH" --ftp-pret -l -B -a --crlf -Q NOOP -Q -DELE x --create-file-mode 0600 --connect-timeout 3 --path-as-is -R -m 9 -z "Sun, 06 Nov 1994 08:49:37 GMT" tftp://example.com/x. Curl.Cli's CommandLineParser now refuses an option in this list that it is expected to accept. I did not isolate which option; the refusal is in Curl.Cli.UnitLibrary.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests --filter "FullyQualifiedName~TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine"
```

- Expected: Passed: 1, Failed: 0
- Actual: Failed: 1 - Assert.IsTrue(parsed.IsAccepted) at TransferContextFactoryTests.cs:942, actual false

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
