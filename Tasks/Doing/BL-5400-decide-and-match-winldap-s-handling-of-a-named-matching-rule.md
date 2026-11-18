---
id: BL-5400
title: Decide and match WinLDAP's handling of a named matching rule in an extensible-match filter
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapWinLdapNamedMatchingRuleFilterTests.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-17
completed:
---
# BL-5400 — Decide and match WinLDAP's handling of a named matching rule in an extensible-match filter

## Goal

A deliberate, recorded decision on how the WinLDAP dialect encodes an extensible-match filter with a named matching rule (`(cn:caseExactMatch:=a)`), implemented and tested to match it.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-108 (Low: same results and exit code, wire bytes differ; REF's behaviour is arguably lossy, so decide deliberately). Confidence: Confirmed (2 shapes, twice). Area: LDAP search filter (WinLDAP dialect), Windows.

**Repro.** The server from STD-106 (an LDAP server on 127.0.0.1:41024 that accepts a simple bind, answers any search, and logs the SearchRequest filter bytes).
`curl -sS "ldap://127.0.0.1:41024/dc=x??base?(cn:caseExactMatch:=a)" -u cn=r:pw`; also `(:caseExactMatch:=a)`.
**Expected (REF/WinLDAP).** extensibleMatch without matchingRule: `8202636e830161840100` and `830161840100`.
**Actual (PORT).** Includes `810e6361736545786163744d61746368` (matchingRule `caseExactMatch`). The OID form `(cn:dn:2.5.13.5:=a)` matches REF exactly.
**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapFilterEncoder.cs`, the extensible-match encoding. ADRs live in `Documentation/Planning/Decisions` (next free number after the highest `ADR-####` there).

## Acceptance criteria

- [ ] An ADR in `Documentation/Planning/Decisions`, marked "Decided by Claude under Stewart's delegation", records the choice: match WinLDAP's lossy encoding (drop a non-OID matching rule name in the WinLDAP dialect) or keep sending the matchingRule element, with the reason and the REF bytes measured.
- [ ] If the decision is to match WinLDAP: new tests in `Curl.Original.Protocol.Ldap.UnitTests` (a new file) prove `(cn:caseExactMatch:=a)` encodes as `8202636e830161840100` and `(:caseExactMatch:=a)` as `830161840100` in the WinLDAP dialect.
- [ ] If the decision is to keep the matchingRule: a test pins the current bytes (including `810e6361736545786163744d61746368`) so the difference is deliberate and recorded.
- [ ] A test proves the OID form `(cn:dn:2.5.13.5:=a)` is unchanged, and that the non-WinLDAP (OpenLDAP) dialect keeps sending a named matching rule.
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [ ] Hand check recorded in the Log: the server logs the bytes the ADR names for the first command.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- `touches` corrected: the review and the task named `Documentation/Decisions`, which does not exist; the ADR folder is `Documentation/Planning/Decisions`.
- Review acceptance check: the server logs `8202636e830161840100` for the first command, or an ADR records the deliberate difference.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 23263171 3c629008 d765c2ef 1dae744e 7ae10ef7 a0b49a29 45f999f9.
