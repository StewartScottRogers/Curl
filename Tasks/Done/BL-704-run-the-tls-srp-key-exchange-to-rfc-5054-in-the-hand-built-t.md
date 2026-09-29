---
id: BL-704
title: Run the TLS-SRP key exchange to RFC 5054 in the hand-built TLS client
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-703]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-704 — Run the TLS-SRP key exchange to RFC 5054 in the hand-built TLS client

## Goal

The hand-built TLS 1.2 client authenticates with a user name and password through the SRP key exchange (RFC 5054: the `srp` extension, SRP ServerKeyExchange with N, g, s, B, the client's A and premaster secret, the RFC 5054 groups, and the SRP suites BL-695's ADR lists), so `--tlsuser`, `--tlspassword` and `--tlsauthtype SRP` work (BL-712).

## Context

- Design: BL-695's ADR. Builds on BL-703. `System.Numerics.BigInteger` for the modular arithmetic, `SHA1` for SRP-6a's hashes (RFC 5054 section 2.5), and a check that the server's group is one of Appendix A's.
- curl's `--tlsauthtype`: "Set the TLS authentication type. Several TLS authentication types are supported: SRP." (https://curl.se/docs/manpage.html, checked 2026-09-28).
- Reference: RFC 5054 Appendix B (the SRP test vectors: I, P, s, k, x, v, a, b, A, B, u, premaster secret).

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` reproduce every RFC 5054 Appendix B value, complete an SRP handshake with an in-memory server for each SRP suite, and fail a wrong password (the server's Finished does not verify), a group not in Appendix A and `B mod N == 0` with typed alerts.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: `SrpGroup` (RFC 5054 Appendix A, taken from the RFC text by script and pinned by SHA-256 of each prime) and `SrpClient` (k, x, v, A, u, S over `BigInteger` and `SHA1`); `Tls12KeyExchange.Srp`, `Tls12SrpParameters`, the nine SRP suites in `Tls12CipherSuite`; `Tls12ClientSettings.SrpCredentials` (`TlsSrpCredentials`) adds `srp` after `server_name` and keeps the SRP suites in the hello; `Tls12ClientHandshake.AgreeSrp` checks the group and B and computes A and S.
- Decisions (ADR-0229, decided by Claude under Stewart's delegation): match OpenSSL 3.5 - SRP suites offered only with credentials; a group outside Appendix A is `insufficient_security` (`SRP_check_known_gN_param`); B of 0 or not below N is `illegal_parameter` (RFC 5054 2.5.4, `srp_verify_server_param`); a is 48 random bytes; S has no leading zeros (`BN_bn2bin`); user name and password as UTF-8 without SASLprep.
- The server-choice check now tests the suite against the ClientHello actually sent (it used the settings' list), so a server choosing an SRP suite the hello left out is `illegal_parameter`; the combined TLS 1.3 hello (ADR-0205) now carries `LowerVersions.OfferedCipherSuites` too.
- Touches: added `Documentation/Planning/Decisions` for ADR-0229 and its index row; no task in Doing names it.
- Tests: `SrpClientTests` (every Appendix B value, the group table), `Tls12SrpHandshakeTests` (all nine suites, groups 1536/2048/3072/8192, TLS 1.0 and 1.1, the hello, wrong password `decrypt_error`, unknown group and wrong generator `insufficient_security`, B = 0, N and above N `illegal_parameter`). Curl.Tls.UnitTests 1106 passed; `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The hand-built TLS 1.2 client runs TLS-SRP (RFC 5054): nine SRP suites, Appendix A groups, typed alerts for a wrong password, unknown group and bad B
