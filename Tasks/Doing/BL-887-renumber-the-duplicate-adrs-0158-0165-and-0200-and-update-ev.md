---
id: BL-887
title: Renumber the duplicate ADRs 0158, 0165 and 0200 and update every reference
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Curl.Tls.UnitLibrary, Curl.Http3.UnitLibrary, Curl.Quic.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-887 — Renumber the duplicate ADRs 0158, 0165 and 0200 and update every reference

## Goal

ADR numbers 0158, 0165 and 0200 each name exactly one record in `Documentation/Planning/Decisions`: in each pair the record fewer references cite gets the next free number, its title line and file name say so, `README.md` indexes both, and every reference points at the record it meant.

## Context

- Parallel lanes numbered ADRs from their own copies of the folder (found by BL-886, 2026-09-29). The pairs:
  - `ADR-0158-the-hand-built-kerberos-reads-version-4-credential-caches-and-version-2-keytabs.md` and `ADR-0158-tls-1-2-and-below-run-over-a-byte-stream-in-their-own-connection-and-stream.md`
  - `ADR-0165-http-3-framing-sends-nghttp3s-default-settings-and-refuses-pushes-as-a-client-that-never-sends-max-push-id.md` and `ADR-0165-the-quic-client-handshake-is-an-io-free-state-machine-that-maps-each-failure-to-one-exit.md`
  - `ADR-0200-smb-and-smbs-speak-curls-smbv1-nt-lm-0-12-on-every-platform.md` and `ADR-0200-the-hand-built-kerberos-follows-cross-realm-referrals-in-the-tgs-exchange.md`
- BL-663 owns the pairs 0086, 0088, 0093 and 0109; BL-886 owns 0187 and 0193.
- `README.md` indexes one row of 0158 and 0165 only, and the SMB 0200 row sits below the `## Template` section rather than in the index.
- Find references with `git grep -n "ADR-0158\b"` (and 0165, 0200) outside `Tasks/Done/<timestamp>/`; read each to tell which record it means. The files that cite them today are in `touches`, plus other ADRs in the folder and Backlog task files. Code changes are comments only.

## Acceptance criteria

- [ ] `Get-ChildItem Documentation/Planning/Decisions -Filter 'ADR-01[56]*.md'` and `-Filter 'ADR-0200*.md'` show 0158, 0165 and 0200 once each.
- [ ] `Documentation/Planning/Decisions/README.md` has one row per record of the three pairs, in number order in the index.
- [ ] A search for each renumbered record's old number finds only references to the record that kept it; each renumbered record's references use its new number.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
