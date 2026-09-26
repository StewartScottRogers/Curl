---
id: BL-084
title: Send the -u user name as NEW-ENVIRON USER on telnet
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-044]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-084 — Send the -u user name as NEW-ENVIRON USER on telnet

## Goal

With `-u user:password` on a `telnet://` URL, the telnet handler offers NEW-ENVIRON and
sends `USER,<user>` ahead of any `-t NEW_ENV` variables, as curl 8.21.0 does.

## Context

Upstream `lib/telnet.c` (`check_telnet_options`) prepends `USER,<user name>` to the
NEW-ENVIRON variables when a user name was given, and refuses a non-ASCII user name
with exit 43. BL-044 implemented `NEW_ENV` but not this; `ITransferContext.Credentials`
carries the user name. Measure against the local curl 8.21.0 with a loopback listener
(see BL-044's Notes): the offer, the `IS` list order with `-t NEW_ENV=` also given, and
the non-ASCII case.

## Acceptance criteria

- [ ] The bytes curl 8.21.0 sends for `-u bob:x` against a server sending
      `IAC DO NEW-ENVIRON` then `IAC SB NEW-ENVIRON SEND IAC SE` are recorded in `Notes`
      and pinned by a named test.
- [ ] The order with both `-u` and `-t NEW_ENV=TERM,vt100` is measured and pinned.
- [ ] A non-ASCII user name is measured and pinned (exit code, message, bytes sent).
- [ ] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Filed by BL-044 as follow-up work.

## Log

- 2026-09-26: Created.
