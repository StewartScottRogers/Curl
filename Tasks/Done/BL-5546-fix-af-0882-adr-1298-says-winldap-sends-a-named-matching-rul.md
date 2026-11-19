---
id: BL-5546
title: Fix AF-0882: ADR-1298 says WinLDAP sends a named matching rule that follows dn:, but the encoder drops it
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-1298-winldap-drops-a-named-matching-rule-in-an-extensible-match-filter.md]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5546 — Fix AF-0882: ADR-1298 says WinLDAP sends a named matching rule that follows dn:, but the encoder drops it

## Goal

The defect the audit office reported as AF-0882 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0882 (High, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Documentation/Planning/Decisions/ADR-1298-winldap-drops-a-named-matching-rule-in-an-extensible-match-filter.md:17`

Location: `Documentation/Planning/Decisions/ADR-1298-winldap-drops-a-named-matching-rule-in-an-extensible-match-filter.md:17`

ADR-1298 (Accepted) Decision: 'In the WinLDAP dialect the matching rule is sent only when it is an OID (starts with a digit) or follows `dn:`; any other name is dropped.' The code drops a named rule with or without dn:. Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs:398-403: '/// <summary>Whether WinLDAP loses the rule: a name (not an OID), with or without a leading <c>dn:</c>, is left out of the wire form (BL-5400, ADR-1298, BL-5453).</summary> private static bool IsNamedRule(string rest) { string rule = rest.StartsWith("dn:", ...) ? rest[3..] : rest; return rule.Length == 0 || !char.IsAsciiDigit(rule[0]); }'. Line 374 uses it: 'string? rule = rest.Length == 0 || dnOnly || IsNamedRule(rest) ? null : rest;'. Curl.Original.Protocol.Ldap.UnitTests/LdapWinLdapNamedMatchingRuleFilterTests.cs:26 pins '(cn:dn:caseExactMatch:=a)' -> '8202636E830161' (no 0x81 rule element). No later ADR records the BL-5453 change. An agent following the ADR would put the dn:-prefixed named rule back on the wire. The EncodeWinLdapExtensible summary (line 358-360, 'the rest is the matching rule unless it is just dn') also omits the drop.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1298-*.md,Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs -Pattern 'or follows `dn:`; any other name is dropped|with or without a leading <c>dn:</c>, is left out' | ForEach-Object { "$($_.Filename):$($_.LineNumber)" }
```

- Expected: The ADR and the code agree: either the ADR says a named rule is dropped after dn: too, or the code sends it.
- Actual: Both match: ADR-1298 says a rule after dn: is sent; LdapFilterEncoder.cs:398 says a named rule with or without dn: is left out.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

ADR-1298 Decision now says a named rule is dropped with or without dn: (BL-5453). Docs-only change; the EncodeWinLdapExtensible summary in LdapFilterEncoder.cs is outside this task's touches, so left to a follow-up.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-19: Doing -> Done. ADR-1298 now states named rules are dropped with or without dn:
