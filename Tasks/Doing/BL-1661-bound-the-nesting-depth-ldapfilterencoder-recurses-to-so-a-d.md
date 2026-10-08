---
id: BL-1661
title: Bound the nesting depth LdapFilterEncoder recurses to, so a deeply nested LDAP URL filter cannot overflow the stack
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ldap.UnitLibrary, Curl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1661 — Bound the nesting depth LdapFilterEncoder recurses to, so a deeply nested LDAP URL filter cannot overflow the stack

## Goal

`LdapFilterEncoder.Encode` refuses (returns `null`, so the transfer fails the way a bad filter does) a filter nested deeper than a fixed limit, instead of recursing without bound and overflowing the stack.

## Context

- Found by BL-1512's adversarial review of `Curl.Protocol.Ldap.UnitLibrary`, by inspection: `ParseSet` -> `ParseParenthesized` -> `ParseAfterParenthesis` -> `ParseSet` recurses once per `(&`, `(|` or `(!` level with no depth limit. A filter such as `"(&"` repeated 1,000,000 times + `(cn=a)` + `")"` repeated 1,000,000 times in an LDAP URL would overflow the stack, and a stack overflow kills the process (it cannot be caught). Not reproduced in-process, because the overflow would take the test host down with it; BL-1512's `LdapAdversarialTests.Encode_FilterNestedTwoHundredDeep_EncodesOneConstructedPerLevel` pins that 200 levels still encode.
- Decide the limit (or an iterative parser) and record it in an ADR; check what real curl does with a deeply nested filter (OpenLDAP's `ldap_pvt_put_filter` and WinLDAP), measuring with `Record-CurlExchange.ps1` where a loopback LDAP exchange allows it.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ldap.UnitTests` encodes a filter nested 100,000 levels deep without crashing the test host and gets the refusal (or the encoding) the ADR decides.
- [ ] `LdapAdversarialTests.Encode_FilterNestedTwoHundredDeep_EncodesOneConstructedPerLevel` still passes.
- [ ] `Curl.Protocol.Ldap.UnitLibrary` stays at 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
