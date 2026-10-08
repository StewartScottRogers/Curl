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
completed: 2026-10-07
---
# BL-1661 — Bound the nesting depth LdapFilterEncoder recurses to, so a deeply nested LDAP URL filter cannot overflow the stack

## Goal

`LdapFilterEncoder.Encode` refuses (returns `null`, so the transfer fails the way a bad filter does) a filter nested deeper than a fixed limit, instead of recursing without bound and overflowing the stack.

## Context

- Found by BL-1512's adversarial review of `Curl.Protocol.Ldap.UnitLibrary`, by inspection: `ParseSet` -> `ParseParenthesized` -> `ParseAfterParenthesis` -> `ParseSet` recurses once per `(&`, `(|` or `(!` level with no depth limit. A filter such as `"(&"` repeated 1,000,000 times + `(cn=a)` + `")"` repeated 1,000,000 times in an LDAP URL would overflow the stack, and a stack overflow kills the process (it cannot be caught). Not reproduced in-process, because the overflow would take the test host down with it; BL-1512's `LdapAdversarialTests.Encode_FilterNestedTwoHundredDeep_EncodesOneConstructedPerLevel` pins that 200 levels still encode.
- Decide the limit (or an iterative parser) and record it in an ADR; check what real curl does with a deeply nested filter (OpenLDAP's `ldap_pvt_put_filter` and WinLDAP), measuring with `Record-CurlExchange.ps1` where a loopback LDAP exchange allows it.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Ldap.UnitTests` encodes a filter nested 100,000 levels deep without crashing the test host and gets the refusal (or the encoding) the ADR decides.
- [x] `LdapAdversarialTests.Encode_FilterNestedTwoHundredDeep_EncodesOneConstructedPerLevel` still passes.
- [x] `Curl.Protocol.Ldap.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- Decided a fixed limit, not an iterative parser (ADR-0425): `LdapFilterEncoder.MaximumSetDepth` = 256 nested `&`/`|`/`!` sets, both dialects; a 257th refuses the filter (`null`), the same failure as any filter the build refuses. Depth is counted, not the number of sets.
- Real curl not measured: OpenLDAP's `ldap_pvt_put_filter`/`put_complex_filter` recurse without a depth check, so a deep enough filter crashes the reference build too; a crash is not behaviour to match, and no real filter nests near 256.
- The check lives in its own `ParseNestedSet` so `ParseSet` stays at complexity 10. Measure-CodeQuality on the library: 100% line, 100% branch, 0 failing members.
- Tests added in `LdapAdversarialTests`: 100,000 deep refused (both dialects), 256 encodes and 257 refused (both dialects), many shallow sibling sets encode, the encoder is reusable after a depth refusal.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. LDAP filters nest at most 256 sets (ADR-0425); deeper ones are refused instead of overflowing the stack
