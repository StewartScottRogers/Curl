---
id: BL-2840
title: Decide how upstream's unittest cases run against Curl and which have no C# counterpart (ADR)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-10-19
completed:
---
# BL-2840 — Decide how upstream's unittest cases run against Curl and which have no C# counterpart (ADR)

## Goal

One ADR, "Decided by Claude under Stewart's delegation", says how upstream's 84 `unittest` cases run against Curl, where their C# ports live, and for every case whether it is ported (and by which task) or has no C# counterpart (and why), so BL-2841 to BL-2849 can start without a question.

## Context

Upstream curl 8.22.0's `tests/data` holds 84 cases that exercise libcurl's or the tool's *internal* C functions and data structures, not the command line. They name the feature `unittest`; upstream builds them as `tests/unit/unit<n>` programs, or as `tests/tunit/tool<n>` programs for the tool's own functions (cases 1394, 1604, 1621, 1622, 1623 and 1720 name `<tool>tool%TESTNUMBER</tool>`; case 1306's `<tool>` is `unit1305`). The numbers are: 1300, 1302, 1303, 1304, 1305, 1306, 1309, 1323, 1330, 1394, 1395, 1397, 1399, 1600 to 1608, 1610 to 1612, 1614 to 1616, 1620 to 1623, 1625 to 1627, 1636, 1650 to 1658, 1660, 1661, 1663, 1664, 1666 to 1669, 1674 to 1676, 1720, 1961, 1979, 1980, 2413, 2600 to 2606, 3200, 3205, 3211 to 3214, 3216, 3219, 3227, 3300 to 3304, 3306 and 3400 (the batch tasks BL-2842 to BL-2849 name each number once; 1306 is in BL-2849). Measured 2026-10-19: the harness skips all of them (`Curl lacks the feature unittest`, or `the harness does not act on <client><tool>`); the gap office has them `excluded | debug-build-only` and, for 1306, `libcurl-unit-test`.

The existing precedent is `Curl.Conformance.LibTest.UnitLibrary`: C# ports of upstream's `tests/libtest` programs written over the public `Libcurl` API only (its `CLAUDE.md` forbids it to reference a protocol library, `Curl.Api` or the conformance library), which already includes four `unitcheck.h`-style programs (`Lib1301`, `Lib1308`, `Lib1396`, `Lib1398`, with `LibTestAssertions`). The unit programs test things `Libcurl` does not expose (hash functions, ASN.1 parsers, the URL normaliser's helpers, alt-svc and HSTS caches, the HTTP/1 parser, DoH encoders), so their ports must reach the libraries that implement them (`Curl.Cryptography`/`Curl.Authentication`, `Curl.Networking`, `Curl.Http`..., find each with the `Glossary` and `rg`). The ADR decides:

1. The mechanism: how the runner recognises a case that is a unit program (the `unittest` feature plus a `unit<n>`/`tool<n>` program name), what the screening reports for it when no port exists, and what `Debug`/`TrackMemory` features a `unittest` case implies (several also name `Debug`, `TrackMemory`, `http`, `sftp` or `DoH`).
2. The home of the ports: proposed new projects `Curl.Conformance.UnitProgram.UnitLibrary` and `Curl.Conformance.UnitProgram.UnitTests` (Directory layout: flat, names sort `.UnitTests` after the library; held to the four quality gates and AOT rules as every `*.UnitLibrary`). If the ADR picks other names, this task edits `touches` and Goal of BL-2841 to BL-2848 to match.
3. The classification of the 84 cases into the batches the board already has (BL-2842 hashes and encodings, BL-2843 ASN.1/X.509/TLS names, BL-2844 URL and address parsing, BL-2845 tool and protocol parsers, BL-2846 headers/alt-svc/HSTS/netrc/HTTP1, BL-2847 DoH/DNS/peers/TLS settings, BL-2848 time and limits, BL-2849 private C data structures) with, per case, the Curl type its port exercises or the reason no counterpart exists. A reason is lasting only where the case tests a C data structure or allocator that Curl does not have (a linked list, splay tree, hash table, bufq, thread pool, connection filter, memory tracker); it is never "hard" or "BCL has no primitive" (root CLAUDE.md, "Decisions").

Upstream: the source of each program is in https://github.com/curl/curl/tree/curl-8_22_0/tests/unit and `/tests/tunit` (curl 8.22.0).

## Acceptance criteria

- [ ] A new `Documentation/Planning/Decisions/ADR-####-*.md`, marked "Decided by Claude under Stewart's delegation", states decisions 1 and 2 above and the reasons.
- [ ] The ADR holds a table with one row for each of the 84 cases (every number once, none missing; check the count against the list in this task) giving `port in BL-####` plus the Curl type it exercises, or `no counterpart` plus a one-sentence reason.
- [ ] No row's reason is that the case is hard to port or that the BCL lacks a primitive; each `no counterpart` row names the C-only thing the case tests.
- [ ] `Documentation/Wiki/Glossary.md` has a "unit program" entry (and the ADR is linked from `Documentation/Planning/Decisions/README.md` if that index lists ADRs).
- [ ] If the batches or project names differ from this task's proposal, BL-2841 to BL-2849 are edited (in Backlog, by hand, keeping their criteria meaningful) in the same commit.

## Notes

## Log

- 2026-10-19: Created.
