---
id: BL-2089
title: Leave AUTH= login options out of the IMAP connection reuse key (upstream test1982)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2089 — Leave AUTH= login options out of the IMAP connection reuse key (upstream test1982)

## Goal

Curl reuses an IMAP connection when only the `;AUTH=` login options of the URLs differ, as curl 8.21.0 does, so upstream test1982 passes.

## Context

`ImapKeptConnection.LoginKeyOf` (Curl.Protocol.Imap.UnitLibrary/ImapKeptConnection.cs, near line 57) puts the login options (`context.Mail?.LoginOptions ?? context.Url.Options`) into the reuse key, so a second URL with different `;AUTH=` opens a new connection and logs out of the first. curl 8.21.0 reuses the connection when only those options differ. Read 8.21.0 `tests/data/test1982` and measure real curl with `Record-CurlExchange.ps1` first; keep user, password and TLS level in the key, and correct the doc comment of `LoginKeyOf`, which says login options count.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2080. Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [x] A new MSTest in Curl.Protocol.Imap.UnitTests (no network) shows two transfers differing only in `;AUTH=` give the same key and `Serves` is true, while a different user, password or TLS level does not.
- [x] A session-level test replays test1982's URLs and shows one CAPABILITY/LOGIN and no LOGOUT between them, matching the bytes test1982 expects.
- [x] Add test1982 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- Behaviour pinned from upstream 8.21.0 tests/data/test1982, whose <protocol> block is real curl's recorded exchange (one CAPABILITY/LOGIN, A005 UID FETCH on the same connection, LOGOUT only at exit); no separate Record-CurlExchange.ps1 run, to stay inside the cost cap. `LoginKeyOf` now keys on user, password and TLS level only; its doc comment says so.
- Added Curl.Conformance.UnitTests to `touches` for the PassingUpstreamCases.txt line; no other task in Doing on origin/work/dark-factory names it.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. IMAP reuse key ignores ;AUTH= options; test1982 passes in process
