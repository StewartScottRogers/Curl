---
id: BL-2041
title: Land BL-1976's shelved SOCKS and --interface work so GF-0046's cases use the proxy in process
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1997, BL-2035]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console]
requirement: none
created: 2026-10-10
completed:
---
# BL-2041 — Land BL-1976's shelved SOCKS and --interface work so GF-0046's cases use the proxy in process

## Goal

BL-1976's goal is met: in process, SOCKS4/5 proxies and --interface are used, so GF-0046's upstream cases measure as curl 8.21.0 does.

## Context

BL-1976 (now Deferred; read its Goal, Acceptance criteria and Notes first) hit the factory's per-task cost cap after most of its work was done. That work is shelved in stash 252932ba6 ("darkfactory BL-1976 20261010-161355"). Apply it by hash (`git stash apply 252932ba6`), never pop, and resolve any conflict with what landed since. BL-2035, the test713 harness work it waited on, is Done. Keep this run lean: finish what BL-1976's Notes say is left, and do not widen it.

## Acceptance criteria

- [ ] Every acceptance criterion of BL-1976 is met. Copy each one here, ticked, as you verify it.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
