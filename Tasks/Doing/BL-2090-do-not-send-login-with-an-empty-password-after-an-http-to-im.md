---
id: BL-2090
title: Do not send LOGIN with an empty password after an HTTP-to-IMAP redirect (upstream test779)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2090 — Do not send LOGIN with an empty password after an HTTP-to-IMAP redirect (upstream test779)

## Goal

After an HTTP-to-IMAP redirect Curl sends nothing after `B001 CAPABILITY` and exits 67, as curl 8.21.0 does, instead of sending `B002 LOGIN v ` with an empty password, so upstream test779 passes.

## Context

In upstream test779 an HTTP response redirects (`--location` with `--oauth2-bearer`) to an IMAP URL; curl sends no login and exits 67 (login denied). Curl sends `B002 LOGIN v ` with an empty password. The cause is UNCONFIRMED; suspects: `ImapAuthentication.MayLogIn` (Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs near line 234) and the redirect dropping the --oauth2-bearer token at Curl.Core.UnitLibrary/RedirectFollower.cs near line 594. Confirm the cause first with a failing unit test, then fix the right place. Read 8.21.0 `tests/data/test779` and measure real curl with `Record-CurlExchange.ps1` to pin the bytes.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2080. Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [ ] The confirmed cause (which suspect, or another) is written in the task Notes, backed by a failing-then-passing MSTest.
- [ ] A new MSTest (Curl.Protocol.Imap.UnitTests and/or Curl.Core.UnitTests, no network) shows the IMAP session sends no command after CAPABILITY when no usable credential exists and the transfer returns `CurlExitCode` 67, with the bytes test779 expects.
- [ ] Add test779 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
