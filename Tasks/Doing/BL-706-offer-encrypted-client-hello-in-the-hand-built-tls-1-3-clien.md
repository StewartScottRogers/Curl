---
id: BL-706
title: Offer Encrypted Client Hello in the hand-built TLS 1.3 client
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-699, BL-677]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-706 — Offer Encrypted Client Hello in the hand-built TLS 1.3 client

## Goal

The hand-built TLS 1.3 client offers Encrypted Client Hello from an ECHConfigList (outer and inner ClientHello, HPKE-sealed payload, `encrypted_client_hello` extension, acceptance confirmation, retry configs on rejection) and sends GREASE ECH when asked, so `--ech` (BL-711) works in every mode curl offers.

## Context

- curl: `--ech` "Specify how to do ECH (Encrypted Client Hello). Supports options including false, grease, true, hard, and configuration values." (https://curl.se/docs/manpage.html, curl 8.23.0, checked 2026-09-28). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28).
- Design: BL-695's ADR (which cites the current ECH specification). Builds on BL-699; HPKE from BL-677.
- Test vectors: the ECH specification's test vectors if it has them; otherwise an in-memory ECH-capable server in the tests built from the same pieces with a fixed HPKE key, and the inner/outer ClientHello bytes pinned for fixed randoms.

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` complete an accepted ECH handshake with the in-memory server (the server sees the inner SNI), handle rejection with retry configs and without, and pin GREASE ECH bytes for fixed randoms; a malformed ECHConfigList is a typed failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
