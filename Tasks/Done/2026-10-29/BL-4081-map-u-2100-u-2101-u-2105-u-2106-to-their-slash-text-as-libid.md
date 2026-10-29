---
id: BL-4081
title: Map U+2100 U+2101 U+2105 U+2106 to their slash text as libidn2 does instead of refusing them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHostNameMapping.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingStd3Tests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4081 — Map U+2100 U+2101 U+2105 U+2106 to their slash text as libidn2 does instead of refusing them

## Goal

Curl converts a host holding U+2100, U+2101, U+2105 or U+2106 to its slash text as curl 8.18.0 with libidn2 2.3.8 does (`a℀b.test` becomes `aa/cb.test`), and no longer refuses it.

## Context

BL-4076 made `CurlUrlHostNameMapping.Map` replace any mapping that holds an ASCII character other than a letter, digit, hyphen or dot with U+FFFD, so `CurlUrlHost` refuses the host. BL-4077's measurement (WSL Ubuntu, curl 8.18.0, libidn2 2.3.8, `LC_ALL=C.utf8`) shows libidn2 does not refuse the slash mappings: `curl --connect-to ::127.0.0.1:PORT http://a℀b.test/` sent `GET / HTTP/1.1` with `Host: aa/cb.test` (exit 52 from an empty socat reply), and without a proxy or connect-to the run failed only at name resolution (exit 6). Only through a proxy does curl fail, with exit 27 (`Out of memory`), and no byte is sent; that part is BL-4080. The Arabic ligatures and isolated forms (U+FDFA, U+FDFB, U+FE70..U+FE7E even) map to text with a space and curl does refuse them with exit 3, so keep that refusal. BL-4076 also refuses other STD3-mapped code points it did not measure (U+FC5E..U+FC63, U+FE10, U+FE33): measure them with the same method and keep or drop each refusal to match.

## Acceptance criteria

- [x] `CurlUrlHost.TryNormalizeReadingHost` accepts `a℀b.test`, `a℁b.test`, `a℅b.test` and `a℆b.test` and gives `aa/cb.test`, `aa/sb.test`, `ac/ob.test` and `ac/ub.test`, each pinned by a test.
- [x] The hosts with U+FDFA, U+FDFB and U+FE70..U+FE7E (even) are still refused, and each other code point BL-4076 refused is measured, with its curl result recorded under Notes and pinned by a test.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-4077's run, which found curl does not refuse these four hosts.
- Pipeline `feature` run directly in this lane (a one-method change with its measurement already the task's method); decision recorded in ADR-1000.
- Measured (WSL Ubuntu, curl 8.18.0, libidn2 2.3.8, `LC_ALL=C.utf8`, `--connect-to` a socat listener): U+2100/2101/2105/2106 send `Host: aa/cb.test`, `aa/sb.test`, `ac/ob.test`, `ac/ub.test`; U+FE10 sends `a,b.test`, U+FE33 `a_b.test` (BL-4076 refused both: refusal dropped); U+FC5E..U+FC63, U+FDFA, U+FE70 exit 3 (refusal kept). Also U+2474 `a(1)b.test`, U+FF0F `a/b.test`, U+00A0 and U+3000 `a b.test`, but `a℀éb`, `a` U+00A0 `éb`, `a︐éb` exit 3 while a literal `a_éb` is `xn--a_b-cma` and `a℀.éb` is `aa/c.xn--b-9fa`: libidn2 refuses such a mapping only in a label that stays outside ASCII, which `Map` now does.
- Measure-CodeQuality for Curl.Protocol.Abstractions.UnitLibrary: 100% line, one branch (`LabelAt`, the last-label case) uncovered; the last-label test added after it covers it. Fast tests green across the solution.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 6fcecf4c 12204d7a 779790c0.
- 2026-10-29: Doing -> Done. Hosts whose UTS 46 mapping leaves non-LDH ASCII in an ASCII label (a℀b.test is aa/cb.test) are kept as libidn2 does; refused only in a label that stays outside ASCII
