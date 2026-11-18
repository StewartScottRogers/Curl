---
id: AF-0872
title: AzureStorageUrlMapping's summary says a bare az:// host always gets the public suffix and is the account; an IP or localhost host is a path-style emulator
auditor: truthfulness
severity: Medium
status: proposed
reason:
key: truthfulness:Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs:AzureStorageUrlMapping:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0872 - AzureStorageUrlMapping's summary says a bare az:// host always gets the public suffix and is the account; an IP or localhost host is a path-style emulator

## Summary

Medium finding from the truthfulness auditor at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs:4`: AzureStorageUrlMapping's summary says a bare az:// host always gets the public suffix and is the account; an IP or localhost host is a path-style emulator. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs:4`

Summary: 'Maps <c>az://account/container/blob</c> to the Blob service endpoint ... A bare account name gets the public cloud's suffix; a host with a dot is used as it is.' MapBlob (line 31) first checks IsPathStyleHost(url): for an IPv4 or IPv6 host or 'localhost' it keeps the host as it is, sets AccountInPath true, and the first path segment becomes the account (ADR-1291). So az://localhost/c/x is not mapped to localhost.blob.core.windows.net with account 'localhost' as the summary says. The library's CLAUDE.md has the same omission. The extension's own description (AzureStorageExtension.cs:15) does state the emulator rule.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs -Pattern 'A bare account name gets the public cloud.s suffix; a host with a dot is used as it is\.').Count; (Select-String -Path Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs -Pattern 'IsPathStyleHost\(url\)').Count
```

- Expected: The summary also states the IP-address/localhost path-style rule (first count 0, or the summary mentions the emulator)
- Actual: 1, 1: the summary states the suffix rule with no exception while MapBlob applies the path-style exception

## Re-audits

## Log

- 2026-11-18: filed proposed.
