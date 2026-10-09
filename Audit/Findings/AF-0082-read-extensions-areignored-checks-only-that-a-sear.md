---
id: AF-0082
title: Read_Extensions_AreIgnored checks only that a search was parsed, not that the extensions left it unchanged
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
key: quality:Curl.Protocol.Ldap.UnitTests/OpenLdapUrlReaderTests.cs:Read_Extensions_AreIgnored:weak-assertion
reproduction: none
task: BL-1760
tasks: BL-1760
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-08_2315.md, 2026-10-09_0225.md
---
# AF-0082 - Read_Extensions_AreIgnored checks only that a search was parsed, not that the extensions left it unchanged

## Summary

Low finding from the quality auditor at `Curl.Protocol.Ldap.UnitTests/OpenLdapUrlReaderTests.cs:99`: Read_Extensions_AreIgnored checks only that a search was parsed, not that the extensions left it unchanged.

## Evidence

Location: `Curl.Protocol.Ldap.UnitTests/OpenLdapUrlReaderTests.cs:99`

For paths 'x????!' and 'x????a,,b', the only assertion is 'Assert.IsNotNull(Read(path).Search);'. A reader that took the extensions into the filter, scope or attributes would still pass. To show they were ignored, the test should compare the search with the one read from 'x' alone.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ldap.UnitTests/OpenLdapUrlReaderTests.cs -Pattern 'Read_Extensions_AreIgnored' -Context 0,5
```

- Expected: The body compares the parsed search with the search of the same URL without extensions.
- Actual: OpenLdapUrlReaderTests.cs:99: Assert.IsNotNull(Read(path).Search);

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | Ran the Select-String: Read_Extensions_AreIgnored now reads both the URL with extensions and the same URL without them and asserts Assert.AreEqual on BaseObject, CollectionAssert.AreEqual on Attributes, and Assert.AreEqual on Scope and Filter.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Read_Extensions_AreIgnored (OpenLdapUrlReaderTests.cs:96) now parses the same URL without its extensions and asserts equal BaseObject, Attributes (CollectionAssert), Scope and Filter.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
