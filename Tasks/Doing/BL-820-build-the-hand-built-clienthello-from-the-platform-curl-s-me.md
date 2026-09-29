---
id: BL-820
title: Build the hand-built ClientHello from the platform curl's measured profile
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-787, BL-821, BL-786, BL-879, BL-880]
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
- 2026-09-29 (lane 5): cannot start yet. A profile hello cannot be sent by `Curl.Tls` as it stands, and `Curl.Tls` is outside this task's touches: both profiles offer TLS 1.2 suites and `supported_versions` 0304+0303, which `Tls13ClientSettings.Validate` refuses and the TLS 1.3 client cannot continue from (BL-821); OpenSSL offers `compress_certificate` (BL-786, in Doing) and key shares on X25519MLKEM768 with x448 among its groups (filed BL-879); both offer `post_handshake_auth`, which the client cannot answer (filed BL-880). Sending the profile bytes without those would advertise capabilities the client lacks, so a server choosing them would fail a handshake real curl completes. Once they land, map the profile to `Tls13ClientSettings` (`ExtensionOrder`, lists, `FixedExtensions` for the empty or fixed extensions, `SendLegacySessionId`) with `--ciphers`, the version range and `--no-alpn` changing only its lists.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waits on BL-821, BL-786, BL-879 and BL-880: Curl.Tls cannot yet send a profile's hello honestly
- 2026-09-29: Backlog -> Doing.
