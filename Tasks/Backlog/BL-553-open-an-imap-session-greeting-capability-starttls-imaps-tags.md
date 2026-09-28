---
id: BL-553
title: Open an IMAP session: greeting, CAPABILITY, STARTTLS, imaps, tags and LOGOUT
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-534, BL-531]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-553 — Open an IMAP session: greeting, CAPABILITY, STARTTLS, imaps, tags and LOGOUT

## Goal

An `ImapProtocolHandler` in `Curl.Protocol.Imap.UnitLibrary` connects through `IConnector`, reads the greeting and untagged responses, issues tagged commands with curl 8.21.0's tag sequence, sends `CAPABILITY`, upgrades with `STARTTLS` per `--ssl`/`--ssl-reqd` or starts in TLS for `imaps://`, ends with `LOGOUT`, reads literals (`{n}`), and maps every failure to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 34 (Blocker, L; IMAP split: this session, BL-554 auth, BL-555 fetch, BL-556 list/search/custom, BL-557 append, BL-558 registration, BL-559 `-v`).
- Design: BL-533's ADR; contract: BL-534; timeouts: BL-498's ADR; endpoints: BL-515's ADR. `Curl.Protocol.Imap.UnitLibrary/CLAUDE.md` rules apply.
- Measure with `Record-CurlExchange.ps1 -Imap` (BL-531): the default session (record the tag format curl uses), a `* BYE` greeting, a `* PREAUTH` greeting, `STARTTLS` refused under `--ssl` and `--ssl-reqd`, `imaps://` with `-Tls -k`, a tagged `BAD`, and the server closing early.

## Acceptance criteria

- [ ] Measured first as above; request lines, stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Imap.UnitTests` drive the handler through a fake connection replaying the measured bytes and pin client bytes (tags included) and outcome for each case, including a literal split across reads.
- [ ] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
