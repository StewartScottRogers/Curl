---
id: AF-0584
title: ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0584 - ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:513`: ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:513`

Step 0 name-lies. ConnectDestination carries ParseErrorCode (ConnectToMappings.cs:35) but the test asserts only destination.ParseError and IsMapped.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/NetworkingAdversarialTests.cs -Pattern 'ConnectToMap_DestinationInAnInvalidPartition_FailsWithCurlsExit49Text' -Context 0,6
```

- Expected: The body asserts destination.ParseErrorCode is SetoptOptionSyntax (49).
- Actual: Only ParseError and IsMapped are asserted.

## Re-audits

## Log

- 2026-11-03: filed proposed.
