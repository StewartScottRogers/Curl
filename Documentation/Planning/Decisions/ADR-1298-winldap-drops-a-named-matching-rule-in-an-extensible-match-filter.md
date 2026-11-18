# ADR-1298: The WinLDAP dialect drops a named matching rule from an extensible-match filter (BL-5400)

- Status: Accepted
- Date: 2026-11-17
- Decided by Claude under Stewart's delegation.

## Context

The outside fitness review (STD-108) found that `(cn:caseExactMatch:=a)` sent a
matchingRule element (`810e6361736545786163744d61746368`), while WinLDAP sends none. The
reference bytes for the extensibleMatch contents are `8202636e830161840100` for
`(cn:caseExactMatch:=a)` and `830161840100` for `(:caseExactMatch:=a)`: the rule name is
lost, the attribute, value and `dnAttributes` FALSE are kept. This is lossy, but the
task is a drop-in replacement and the server sees the same bytes either way only if we match.

## Decision

In the WinLDAP dialect the matching rule is sent only when it is an OID (starts with a
digit) or follows `dn:`; any other name is dropped. The OID form `(cn:dn:2.5.13.5:=a)` is
unchanged. The libldap (OpenLDAP) dialect keeps sending a named matching rule.
