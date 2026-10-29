---
id: BL-4026
title: Let a ConnectTarget name its TLS SNI host and ask for system-store-only trust (needed by CGR-289)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/ConnectTargetTlsOverrideTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4026 — Let a ConnectTarget name its TLS SNI host and ask for system-store-only trust (needed by CGR-289)

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-28: Created.

## Requirements (filed by the BL-3818 run)

- `ConnectTarget` gains an optional `TlsServerName` (the SNI and certificate-name host when it differs from `Host`) and an optional switch that makes the TLS provider trust only the system store (ignoring `--cacert`, `--capath` and `-k`), both honoured by the Schannel-style provider and the hand-built one. Unit tests in Curl.Networking.UnitTests.
- Why: BL-3818 (CGR-289) needs the Windows ldaps:// path to open WinLDAP's second connection with the machine name as SNI and system-store trust.
