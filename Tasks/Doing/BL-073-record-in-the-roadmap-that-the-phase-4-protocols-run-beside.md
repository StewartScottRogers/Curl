---
id: BL-073
title: Record in the roadmap that the Phase 4 protocols run beside Phase 1, and where composition and TLS sit
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-073 — Record in the roadmap that the Phase 4 protocols run beside Phase 1, and where composition and TLS sit

## Goal

`Documentation/Planning/Roadmap.md` records Stewart's 2026-09-26 decision that the small
Phase 4 protocols are built in parallel with Phase 1, and states where `Curl.Console`
composition and the production TLS transport sit, with the task IDs for each.

## Context

- `Documentation/Planning/Roadmap.md` today has Milestone 0 (in progress) and a
  placeholder Milestone 1; its banner says no milestones are set.
- `Documentation/Product/Product-Overview.md`, Phasing: Phase 1 is `Abstractions`,
  `Networking`, `Core`, `Cli`, `Output`, `Console`, `File` and `Http`; Phase 4 is `Ws`,
  `Mqtt`, `Tftp`, `Dict`, `Gopher`, `Telnet`.
- Stewart decided on 2026-09-26 that DICT, Gopher, Telnet, TFTP and MQTT (not WS) are
  built in parallel with Phase 1 for throughput, now that the dark factory runs parallel
  lanes. Done so far: ADR-0005 and ADR-0006 (BL-032), requirements FR-020 to FR-045
  (BL-033), contracts (BL-034, BL-035), connectors (BL-039, BL-040) and the handlers
  (dict BL-041, gopher BL-042, telnet BL-043, tftp BL-045 and BL-046, mqtt BL-048 and
  BL-049). Check the board (`task-board.ps1 status`) for any finished since.
- Composition (Phase 1, `Curl.Console`): BL-068 (runner, composition root, `file://`),
  BL-069 (connectors), BL-070 (dict, gopher, telnet, tftp and mqtt handlers), BL-071 and
  BL-072 (TLS options).
- TLS (Phase 1, `Curl.Networking.UnitLibrary` behind `ITlsProvider`, per ADR-0005 and the
  Product Overview's "TLS lives once" finding): BL-061 (failures as `ConnectResult`),
  BL-062 (`SslStreamTlsProvider`, `-k`, TLS versions, exits 35 and 60), BL-063
  (`--cacert`), BL-064 (messages and `--capath`), BL-065 (`--cert`/`--key`), BL-066
  (`--ciphers`); command-line parsing in `Curl.Cli.UnitLibrary`, BL-067; Stewart's
  decisions BL-059 (which curl TLS build to match) and BL-060 (`--ciphers` on the BCL).
- HTTP, and the other Phase 1 option groups, are not planned by these tasks.
- Docs only: do not edit `Product-Overview.md`; if its Phasing table should change, say
  so in `Notes` for Stewart.

## Acceptance criteria

- [ ] `Roadmap.md` has a Milestone 1 section for Phase 1 whose `Delivers` names
      `Curl.Console` composition and the production TLS transport with the task IDs
      above, and whose exit criterion includes `curl <url>` running end to end for
      `file`, `dict`, `gopher`, `gophers`, `telnet`, `tftp`, `mqtt` and `mqtts` with
      curl's exit codes.
- [ ] `Roadmap.md` states, dated 2026-09-26 and attributed to Stewart, that DICT, Gopher,
      Telnet, TFTP and MQTT are built in parallel with Phase 1 because the dark factory
      runs parallel lanes, and that WS stays in Phase 4.
- [ ] `Roadmap.md` states that ADR-0005 and ADR-0006 remain Accepted, and that `dict://`
      sends curl's exact `CLIENT libcurl <version>` line.
- [ ] No placeholder text is left in a section this task writes, and no file other than
      `Documentation/Planning/Roadmap.md` changes.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
