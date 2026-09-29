---
id: BL-746
title: Record the --proto scheme-set decisions in an ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-522]
touches: [Documentation/Planning/Decisions, Curl.Cli.UnitLibrary]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-746 — Record the --proto scheme-set decisions in an ADR

## Goal

An ADR in `Documentation/Planning/Decisions`, marked "Decided by Claude under Stewart's delegation", records the decisions BL-522 took when it taught `Curl.Cli.UnitLibrary` to read `--proto`, `--proto-redir` and `--proto-default`.

## Context

- BL-522 could not write the ADR itself: BL-515 held `Documentation/Planning/Decisions` in its `touches` at the time. The decisions and their reasons are in BL-522's Notes.
- Code: `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs` (`KnownSchemes`, `Read`, `KnownScheme`).
- The decisions to record:
  1. The known schemes are one list on every platform: the Windows (Schannel) curl 8.21.0 `Protocols:` line less `ipfs` and `ipns` (which that build's libcurl does not know: `--proto ipfs` warns, `--proto-default ipfs` is refused). The Linux and macOS OpenSSL builds may know more (such as `smb`, `rtmp`), so `--proto smb` warns there where theirs would not; one list keeps tests platform-neutral and output identical everywhere.
  2. Sets are stored lowercase, unordered (`IReadOnlySet<string>`); `null` means the option was not given.
  3. `--proto-default` refuses an unknown scheme (`all` included) with exit 1, as measured.

## Acceptance criteria

- [x] A new `ADR-####-*.md` in `Documentation/Planning/Decisions`, numbered after the highest existing one, states the three decisions above and why, and is marked "Decided by Claude under Stewart's delegation".
- [x] The Decisions `README.md` index (if it lists ADRs) names the new ADR.
- [x] `CommandLineProtocolSet`'s XML summary on `KnownSchemes` cites the ADR number.

## Notes

- Numbered ADR-0189, not 0188: another lane's branch already holds an ADR-0188 (--cert-status, BL-610), so 0188 would collide when the shifts integrate.
- Verified: `dotnet build Curl.Cli.UnitTests` clean; fast tests 2864 passed, 15 skipped, 0 failed.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0189 records the --proto scheme-set decisions; indexed and cited from KnownSchemes
