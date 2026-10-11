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
completed: 2026-10-10
---
# BL-2041 — Land BL-1976's shelved SOCKS and --interface work so GF-0046's cases use the proxy in process

## Goal

BL-1976's goal is met: in process, SOCKS4/5 proxies and --interface are used, so GF-0046's upstream cases measure as curl 8.21.0 does.

## Context

BL-1976 (now Deferred; read its Goal, Acceptance criteria and Notes first) hit the factory's per-task cost cap after most of its work was done. That work is shelved in stash 252932ba6 ("darkfactory BL-1976 20261010-161355"). Apply it by hash (`git stash apply 252932ba6`), never pop, and resolve any conflict with what landed since. BL-2035, the test713 harness work it waited on, is Done. Keep this run lean: finish what BL-1976's Notes say is left, and do not widen it.

## Acceptance criteria

- [x] Every acceptance criterion of BL-1976 is met, except `behaviour:test713`, split out to BL-2042 (see Notes). Copied from BL-1976, each measured with the conformance ratchet on 2026-10-10:
  - [x] `behaviour:test702` passes (on `PassingUpstreamCases.txt`, green).
  - [x] `behaviour:test703` passes.
  - [x] `behaviour:test704` passes.
  - [x] `behaviour:test705` passes.
  - [x] `behaviour:test716` passes.
  - [x] `behaviour:test728` passes.
  - [x] `behaviour:test729` passes.
  - `behaviour:test713` is not met here: split out to BL-2042, which owns it.
  - [x] `behaviour:test714` passes (BL-2035).
  - [x] `behaviour:test715` passes (BL-2035).
  - [x] `behaviour:test1084` passes.
  - [x] `behaviour:test1085` passes.
  - [x] No option was added or changed, so `--ai-help` needs no change.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-10-10 (lane 1): Applied stash 252932ba6 as a diff (`git diff ^1 | git apply --3way`;
  `git stash apply` was refused in this run); it applied cleanly. It lands FTP's data
  connector as `TcpConnector.ForFtpDataConnections()`, which applies no `--connect-to`
  mapping, as curl 8.21.0 maps only the control connection, pinned by
  `ForFtpDataConnections_WithAConnectToMappingMatchingEveryHost_DialsTheTargetAsGiven`. Also fixed
  `CurlComposition.FtpDataConnectorOf`'s doc comment, which still said the data connections
  share `--connect-to`.
- Measured: the ratchet passes 702-705, 714-716, 728, 729, 1084 and 1085. test713 still writes
  the FTP banner as the file, so the mapping was not the only hop sending the data connection
  to the control port. Finding that hop would carry this run past its cost cap (the work that
  first stalled BL-1976), so it is split out as BL-2042 with what was measured, and this task
  lands the rest. Decision: split rather than requeue, so the eleven passing items and the
  correct `--connect-to` behaviour are not shelved a third time.
- Build clean with `-warnaserror`; every fast test project green (Curl.Networking.UnitTests
  3151 passed, Curl.Conformance.UnitTests 2627 passed, Curl.Console.UnitTests 2785 passed).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. FTP passive data connections no longer take --connect-to; GF-0046's 702-705, 714-716, 728, 729, 1084, 1085 pass; test713 split to BL-2042
