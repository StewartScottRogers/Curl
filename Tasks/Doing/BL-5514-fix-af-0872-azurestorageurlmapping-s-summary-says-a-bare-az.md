---
id: BL-5514
title: Fix AF-0872: AzureStorageUrlMapping's summary says a bare az:// host always gets the public suffix and is the account; an IP or localhost host is a path-style emulator
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5514 — Fix AF-0872: AzureStorageUrlMapping's summary says a bare az:// host always gets the public suffix and is the account; an IP or localhost host is a path-style emulator

## Goal

The defect the audit office reported as AF-0872 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0872 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs:4`

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs:4`

Summary: 'Maps <c>az://account/container/blob</c> to the Blob service endpoint ... A bare account name gets the public cloud's suffix; a host with a dot is used as it is.' MapBlob (line 31) first checks IsPathStyleHost(url): for an IPv4 or IPv6 host or 'localhost' it keeps the host as it is, sets AccountInPath true, and the first path segment becomes the account (ADR-1291). So az://localhost/c/x is not mapped to localhost.blob.core.windows.net with account 'localhost' as the summary says. The library's CLAUDE.md has the same omission. The extension's own description (AzureStorageExtension.cs:15) does state the emulator rule.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs -Pattern 'A bare account name gets the public cloud.s suffix; a host with a dot is used as it is\.').Count; (Select-String -Path Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs -Pattern 'IsPathStyleHost\(url\)').Count
```

- Expected: The summary also states the IP-address/localhost path-style rule (first count 0, or the summary mentions the emulator)
- Actual: 1, 1: the summary states the suffix rule with no exception while MapBlob applies the path-style exception

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
