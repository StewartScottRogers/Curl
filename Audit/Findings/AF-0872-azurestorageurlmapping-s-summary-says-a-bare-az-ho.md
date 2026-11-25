---
id: AF-0872
title: AzureStorageUrlMapping's summary says a bare az:// host always gets the public suffix and is the account; an IP or localhost host is a path-style emulator
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs:AzureStorageUrlMapping:false-doc-comment
reproduction: none
task: BL-5514
tasks: BL-5514
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_0602.md
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

- 2026-11-19 | 2026-11-19_0134.md | not re-audited | Ran the reproduction: 1 and 2, presumably the same counts as when filed, because the reproduction only counts a sentence that is still present and the IsPathStyleHost calls. The summary is fixed all the same: AzureStorageUrlMapping.cs lines 7-8 now add 'An IPv4 or IPv6 address or localhost is a path-style emulator host (ADR-1291): the host is kept as it is and the first path segment is the account, not the host.', which matches MapBlob/MapDfs. Because the counts cannot show this, the verdict is not re-audited rather than yes or no.
- 2026-11-19 | 2026-11-19_0602.md | reproduces: no | Ran the reproduction: counts 1 and 2. The summary line 6 still says a bare account gets the public suffix, but lines 7-8 now add 'An IPv4 or IPv6 address or localhost is a path-style emulator host (ADR-1291): the host is kept as it is and the first path segment is the account', which matches IsPathStyleHost(url) at lines 35 and 46.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces.
