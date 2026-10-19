---
id: BL-2849
title: Record why upstream's unit cases for libcurl's private C data structures have no C# counterpart: cases 1300, 1305, 1306, 1309, 1330, 1602, 1603, 1616, 1661, 2600, 2601, 2606, 3211, 3212, 3213, 3300, 3301 and 3306
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2840]
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-10-19
completed:
---
# BL-2849 — Record why upstream's unit cases for libcurl's private C data structures have no C# counterpart: cases 1300, 1305, 1306, 1309, 1330, 1602, 1603, 1616, 1661, 2600, 2601, 2606, 3211, 3212, 3213, 3300, 3301 and 3306

## Goal

Each of upstream cases 1300, 1305, 1306, 1309, 1330, 1602, 1603, 1616, 1661, 2600, 2601, 2606, 3211, 3212, 3213, 3300, 3301 and 3306 is either ported (where Curl has a type with that observable behaviour) or recorded in one ADR with the C-only thing it tests, so none is left unexplained.

## Context

These eighteen `unittest` cases (skipped today with `Curl lacks the feature unittest`; gap scorecard 2026-10-19_1329: `excluded | debug-build-only`, and 1306 `libcurl-unit-test`) test libcurl's private C data structures and allocator in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit): 1300 `Curl_llist` (linked list), 1305 and 1306 (1306's `<tool>` is `unit1305`) the internal hash table, 1602, 1603 and 1616 hash variants (`uint_hash`), 1309 the splay tree, 1330 memory tracking (`TrackMemory`), 1661 `bufref`, 2601 `bufq`, 3211, 3212 and 3213 `uint_bset`, `uint_hashset`, `uint32_tbl`, `uint_spbset`, 3300 and 3301 the thread pool and thread queue, 3306 the thread queue's retry of failed thread starts (`Debug`, sets `CURL_DBG_THRDPOOL_FAIL_STARTS=5`), 2600 connection-filter connect and destroy (`Debug`, `!win32`) and 2606 `Curl_conn_adjust_pollset`. Curl uses .NET collections, `Task`s and `Channel`s instead of these C structures, so most have no counterpart; but a few may name behaviour Curl implements itself (a bounded queue, a thread pool with failing starts): for each case read `tests/unit/unit<n>.c`, and decide with BL-2840's rule (a reason is lasting only where the case tests a C structure or allocator Curl does not have; never "hard").

A case that does have a Curl counterpart is not ported here (the mechanism of BL-2841 may not exist yet): file its port as a new task that depends on BL-2841 and record the row as `ported (task BL-####)`. For the rest, record the row as `no counterpart`.

## Acceptance criteria

- [ ] One ADR, "Decided by Claude under Stewart's delegation", has a row for each of the eighteen cases: `ported (task BL-####)` or `no counterpart` plus the C-only thing it tests (for example "`Curl_llist` is a C intrusive list; Curl uses `List<T>`").
- [ ] No row's reason is that the case is hard or that the BCL lacks a primitive.
- [ ] Every case recorded `ported` has a follow-up task filed that depends on BL-2841 and names the case.
- [ ] The row for each case in the ADR from BL-2840 agrees with this ADR.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-19: Created.
