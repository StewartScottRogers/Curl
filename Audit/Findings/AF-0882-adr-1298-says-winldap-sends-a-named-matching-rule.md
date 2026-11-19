---
id: AF-0882
title: ADR-1298 says WinLDAP sends a named matching rule that follows dn:, but the encoder drops it
auditor: truthfulness
severity: High
status: proposed
reason:
key: truthfulness:Documentation/Planning/Decisions/ADR-1298-winldap-drops-a-named-matching-rule-in-an-extensible-match-filter.md:ADR-1298:stale-adr
reproduction: none
task: none
tasks:
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0882 - ADR-1298 says WinLDAP sends a named matching rule that follows dn:, but the encoder drops it

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1298-winldap-drops-a-named-matching-rule-in-an-extensible-match-filter.md:17`: ADR-1298 says WinLDAP sends a named matching rule that follows dn:, but the encoder drops it.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1298-winldap-drops-a-named-matching-rule-in-an-extensible-match-filter.md:17`

ADR-1298 (Accepted) Decision: 'In the WinLDAP dialect the matching rule is sent only when it is an OID (starts with a digit) or follows `dn:`; any other name is dropped.' The code drops a named rule with or without dn:. Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs:398-403: '/// <summary>Whether WinLDAP loses the rule: a name (not an OID), with or without a leading <c>dn:</c>, is left out of the wire form (BL-5400, ADR-1298, BL-5453).</summary> private static bool IsNamedRule(string rest) { string rule = rest.StartsWith("dn:", ...) ? rest[3..] : rest; return rule.Length == 0 || !char.IsAsciiDigit(rule[0]); }'. Line 374 uses it: 'string? rule = rest.Length == 0 || dnOnly || IsNamedRule(rest) ? null : rest;'. Curl.Original.Protocol.Ldap.UnitTests/LdapWinLdapNamedMatchingRuleFilterTests.cs:26 pins '(cn:dn:caseExactMatch:=a)' -> '8202636E830161' (no 0x81 rule element). No later ADR records the BL-5453 change. An agent following the ADR would put the dn:-prefixed named rule back on the wire. The EncodeWinLdapExtensible summary (line 358-360, 'the rest is the matching rule unless it is just dn') also omits the drop.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1298-*.md,Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs -Pattern 'or follows `dn:`; any other name is dropped|with or without a leading <c>dn:</c>, is left out' | ForEach-Object { "$($_.Filename):$($_.LineNumber)" }
```

- Expected: The ADR and the code agree: either the ADR says a named rule is dropped after dn: too, or the code sends it.
- Actual: Both match: ADR-1298 says a rule after dn: is sent; LdapFilterEncoder.cs:398 says a named rule with or without dn: is left out.

## Re-audits

## Log

- 2026-11-19: filed proposed.
