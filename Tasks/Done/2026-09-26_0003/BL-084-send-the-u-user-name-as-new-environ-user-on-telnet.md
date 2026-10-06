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
completed: 2026-09-26
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

- [x] The bytes curl 8.21.0 sends for `-u bob:x` against a server sending
      `IAC DO NEW-ENVIRON` then `IAC SB NEW-ENVIRON SEND IAC SE` are recorded in `Notes`
      and pinned by a named test.
- [x] The order with both `-u` and `-t NEW_ENV=TERM,vt100` is measured and pinned.
- [x] A non-ASCII user name is measured and pinned (exit code, message, bytes sent).
- [x] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Filed by BL-044 as follow-up work.

### Plan (as built)

- `TelnetOptionParser.Parse` now takes the `-u` user name
  (`ITransferContext.Credentials?.UserName`) first: a non-ASCII one returns exit 43
  before any `-t` option is read; otherwise `USER,<name cut to 250>` is added as the
  first NEW-ENVIRON variable. `TelnetReceiver` needed no change: a non-empty
  variable list already makes it perform and offer NEW-ENVIRON.
- Tests: `TelnetProtocolHandlerUserNameTests` (14 cases). Telnet test project 148
  passing; line and branch coverage of `Curl.Protocol.Telnet.UnitLibrary` 100%.

### Measurements, curl 8.21.0, loopback listener, 2026-09-26 (stdin empty)

Server sends `FF FD 27` then `FF FA 27 01 FF F0` unless stated; `O` = the offers
`FF FB 00 FF FD 00 FF FB 03 FF FD 03`.

- `-u bob:x` → `FF FB 27 O FF FA 27 00 00 55 53 45 52 01 62 6F 62 FF F0`, exit 0.
- `-u bob:x -t NEW_ENV=TERM,vt100` → `FF FB 27 O FF FA 27 00 00 55 53 45 52 01 62 6F 62
  00 54 45 52 4D 01 76 74 31 30 30 FF F0`: `USER` first.
- `-u bob:x`, server sends only `FF FB 01` → `FF FD 01 O FF FB 27`.
- `-u :x` → `USER` with an empty value (`... 55 53 45 52 01 FF F0`); `-u a,b:x` → value
  `61 2C 62`; `-u b%C3%A9:x` → sent literally, not percent-decoded.
- User name of 249 characters → 276 bytes sent; 250, 251, 252 and 300 → 277: the name
  is cut to 250 characters (`USER,` + name in a 256-byte buffer).
- `-u bé:x` → exit 43 `A libcurl function was given a bad argument`, nothing sent; also
  with `-t BOGUS=1` or `-t TTYPE` (the user name is checked before the options).
  `-u bob:x -t TTYPE` → exit 49 as before.

### Choices made unattended

- `-u bob` without a password was not measured: curl prompts for the password on the
  console, which a scripted run cannot answer. The handler sends whatever user name
  `Credentials` carries, so it behaves the same once a password is in hand.
- A user name in the URL (`telnet://bob@host`) is not covered: `Curl.Console` does not
  put URL user info into `Credentials`, which is outside this task's `touches`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. telnet sends the -u user name as NEW-ENVIRON USER ahead of -t NEW_ENV, cut to 250 characters, and refuses a non-ASCII one with exit 43, byte for byte as curl 8.21.0
