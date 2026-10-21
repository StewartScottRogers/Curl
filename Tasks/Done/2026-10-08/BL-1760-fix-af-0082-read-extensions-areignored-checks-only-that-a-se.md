---
id: BL-1760
title: Fix AF-0082: Read_Extensions_AreIgnored checks only that a search was parsed, not that the extensions left it unchanged
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1760 — Fix AF-0082: Read_Extensions_AreIgnored checks only that a search was parsed, not that the extensions left it unchanged

## Goal

The defect the audit office reported as AF-0082 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0082 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0082-read-extensions-areignored-checks-only-that-a-sear.md`.

Location: `Curl.Protocol.Ldap.UnitTests/OpenLdapUrlReaderTests.cs:99`

Location: `Curl.Protocol.Ldap.UnitTests/OpenLdapUrlReaderTests.cs:99`

For paths 'x????!' and 'x????a,,b', the only assertion is 'Assert.IsNotNull(Read(path).Search);'. A reader that took the extensions into the filter, scope or attributes would still pass. To show they were ignored, the test should compare the search with the one read from 'x' alone.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ldap.UnitTests/OpenLdapUrlReaderTests.cs -Pattern 'Read_Extensions_AreIgnored' -Context 0,5
```

- Expected: The body compares the parsed search with the search of the same URL without extensions.
- Actual: OpenLdapUrlReaderTests.cs:99: Assert.IsNotNull(Read(path).Search);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix, done directly rather than through the full feature stages: no production code changes. `Read_Extensions_AreIgnored` now reads the same URL without its extensions and compares base DN, attributes, scope and filter; a third row (`x?cn?sub?(cn=a)?!e,f`) has non-default attributes, scope and filter so a reader leaking extensions into any of them fails. Curl.Protocol.Ldap.UnitTests: 608 passed.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Read_Extensions_AreIgnored compares the search with the one read without extensions (AF-0082)
