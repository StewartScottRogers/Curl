---
id: BL-5907
title: Bind with SASL over ldap:// as OpenLDAP does on Linux: challenge-response, bearer, EXTERNAL, GSSAPI and any-mechanism binds
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5853]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapOpenLdapSaslBind]
requirement: none
created: 2026-11-24
completed:
---
# BL-5907 — Bind with SASL over ldap:// as OpenLDAP does on Linux: challenge-response, bearer, EXTERNAL, GSSAPI and any-mechanism binds

## Goal

On Linux (OpenLDAP-backed curl) the port binds with SASL over `ldap://` as upstream curl 8.21.0 linked against OpenLDAP does: it completes the challenge-response, bearer, `EXTERNAL`, `GSSAPI` and any-mechanism binds (exit 0 with the base entry, or 67 where upstream gives 67), refuses `GSSAPI` for a user naming no realm (exit 67, no bind), and sends the same first bind request for `PLAIN` and `LOGIN`.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 26 (Surl `Ldap.md` findings L-1, L-2 and L-3, one root: the port's SASL bind is not OpenLDAP's). Seen on linux-x64 in Surl's `curl-port-phase-7` runs 44044472038 and 45412760425 at port commit `74f3f45` (2026-11-16). Check what still reproduces on the current branch first: much LDAP SASL work has landed since (BL-5525, BL-5819), and BL-5853 (in Doing) is making OpenLDAP bind with PLAIN when offered, so this task depends on it.

24 cases, the `OpenLdap_SaslBind_*` rows of Surl's `Ldap.md` linux-x64 findings table (mirroring Surl's `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests`):
- L-1 (17 cases: challenge-response, bearer, `EXTERNAL`, `GSSAPI`, `AnyMechanism`): upstream exit 0 with the base entry, or 67; the port exited 38 with nothing on stdout, and its first bind request was shorter (BER length `0x12` against upstream's `0x3E` on bearer).
- L-2 (1 case, `GssapiWithAUserNamingNoRealm`): upstream exit 67 with no bind; the port exited 0 with the base entry (`0x17`).
- L-3 (6 cases, `PlainTextMechanismWithAllowPlaintextAuth` and `WrongPassword`): same exit and output, but the first request's length byte was `0x16`/`0x17` against upstream's `0x3E`.

What decides: curl 8.21.0 manual (`--login-options`, `-u`), RFC 4513 section 5.2, RFC 4422, and OpenLDAP's `ldap_sasl_interactive_bind_s` request bytes. Measure upstream's OpenLDAP build (Linux CI or WSL) with `Record-CurlExchange.ps1` before pinning bytes. If the remaining work is large, split it by L-1 / L-2 / L-3.

## Acceptance criteria

- [ ] For each mechanism group in L-1, L-2 and L-3, the port's first bind request bytes and exit code match upstream's OpenLDAP build, pinned by unit tests in `Curl.Original.Protocol.Ldap.UnitTests` against a test-double LDAP server (marked for the OpenLDAP path, so they pass on every platform).
- [ ] The measured upstream bytes are recorded in Notes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 26.
