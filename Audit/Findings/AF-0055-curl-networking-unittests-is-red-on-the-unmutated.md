---
id: AF-0055
title: Curl.Networking.UnitTests is red on the unmutated tree: LocalBindLines writes 'Could not Resolve host' and the Windows resolve-and-bind test fails
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitTests/TcpConnectorTests.LocalBindLines.cs:ConnectAsync_WithAHostThatDoesNotResolveOnWindows_ReportsTheResolveAndBindFailures:failing-test
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0055 - Curl.Networking.UnitTests is red on the unmutated tree: LocalBindLines writes 'Could not Resolve host' and the Windows resolve-and-bind test fails

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/LocalBindLines.cs:61`: Curl.Networking.UnitTests is red on the unmutated tree: LocalBindLines writes 'Could not Resolve host' and the Windows resolve-and-bind test fails.

## Evidence

Location: `Curl.Networking.UnitLibrary/LocalBindLines.cs:61`

dotnet test Curl.Networking.UnitTests -c Release: Failed: 1, Passed: 3034, Skipped: 28. The failing test is ConnectAsync_WithAHostThatDoesNotResolveOnWindows_ReportsTheResolveAndBindFailures (TcpConnectorTests.LocalBindLines.cs:127): 'CollectionAssert.AreEqual failed ... Expected: r Actual: R'. The test expects curl's line 'Could not resolve host: bogus0'. LocalBindLines.cs:61 is CouldNotResolveHost(string hostName) => $"Could not Resolve host: {hostName}", so --interface bogus0 -v writes a capital R that curl does not. The test catches the defect. Because the twin's baseline is red, Invoke-MutationTest.ps1 needs -ExcludeBaselineFailures, and every Networking mutation run this audit left this test out (excludedTests).

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Networking.UnitTests -c Release -nologo --filter "Name=ConnectAsync_WithAHostThatDoesNotResolveOnWindows_ReportsTheResolveAndBindFailures"
```

- Expected: Passed (on Windows): the line reads 'Could not resolve host: bogus0'.
- Actual: Failed: element mismatch 'r' vs 'R'; LocalBindLines.cs:61 writes 'Could not Resolve host: '.

## Re-audits

## Log

- 2026-10-07: filed proposed.
