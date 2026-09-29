---
id: BL-820
title: Build the hand-built ClientHello from the platform curl's measured profile
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-787]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-820 — Build the hand-built ClientHello from the platform curl's measured profile

## Goal

`HandBuiltTlsProvider` builds its ClientHello from `ClientHelloProfile.Schannel` when it matches the Schannel build and `ClientHelloProfile.OpenSsl` when it matches the OpenSSL build, so a server sees the platform curl's hello whichever TLS client runs (ADR-0140, "Default ClientHello").

## Context

- ADR-0140 gives BL-708 "the profile choice per platform"; the profiles are BL-787's and did not exist when BL-708 ran, so `HandBuiltTlsProvider` (ADR-0162 decision 4) sends `Curl.Tls`'s default `Tls13ClientSettings` and `Tls12ClientSettings` lists.
- Options change a profile's lists, never its extension order: `--ciphers`/`--tls13-ciphers` the suites (see `HandBuiltTlsProvider.SelectOpenSslCipherSuites`), the version range the versions, `--no-alpn` removes ALPN.
- Code: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs` (`ClientSettings`).

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` capture the ClientHello `HandBuiltTlsProvider` sends in each build (a test server that records the first record) and show it is the profile's hello with the target host's `server_name` and the offered ALPN.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-708 (ADR-0162).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
