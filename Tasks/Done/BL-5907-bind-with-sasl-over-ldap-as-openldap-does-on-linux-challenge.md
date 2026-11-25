---
id: BL-5907
title: Bind with SASL over ldap:// as OpenLDAP does on Linux: challenge-response, bearer, EXTERNAL, GSSAPI and any-mechanism binds
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5853]
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapOpenLdapSaslBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapDigestMd5AndPlainSasl.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapProtocolHandler.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapRequests.cs, Curl.Original.Protocol.Ldap.UnitLibrary/LdapAuthenticatedBind.cs, Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapLogonBind.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapOpenLdapSaslBind, Curl.Original.Protocol.Ldap.UnitTests/LdapOpenLdapSaslBindMechanismTests.cs, Curl.Original.Protocol.Ldap.UnitTests/LdapOpenLdapSaslBindTests.cs, Curl.Original.Protocol.Ldap.UnitTests/Curl.Original.Protocol.Ldap.UnitTests.csproj, Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
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

- [x] For each mechanism group in L-1, L-2 and L-3, the port's first bind request bytes and exit code match upstream's OpenLDAP build, pinned by unit tests in `Curl.Original.Protocol.Ldap.UnitTests` against a test-double LDAP server (marked for the OpenLDAP path, so they pass on every platform).
- [x] The measured upstream bytes are recorded in Notes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- ADR-1359. The OpenLDAP SASL bind now runs the shared `ISaslAuthenticator` (given by `EngineComposition`), so CRAM-MD5, NTLM, OAUTHBEARER, XOAUTH2, EXTERNAL, GSSAPI and LOGIN bind; DIGEST-MD5 keeps the LDAP exchange.
- What still reproduced on this branch: L-1 (every mechanism but DIGEST-MD5 and PLAIN was refused); L-3's first request was already the rootDSE search, but it carried time limit 120 where upstream sends 0; the DIGEST-MD5 first bind carried an empty credentials field upstream omits; L-2 already exited 67.
- `touches` widened to the files above: none is named by another task in Doing on origin/work/dark-factory (BL-5915 names `Curl.Engine.UnitTests`, not changed). The LDAP tests now reference `Curl.Authentication.UnitLibrary` so they pin the real mechanism bytes.
- Measured upstream: curl 8.18.0, OpenLDAP 2.6.10 (WSL Ubuntu; the only OpenLDAP build here, not 8.21.0), `Record-CurlExchange.ps1 -Script`, user `u`, password `p`. Every first request is the rootDSE search `30 3e 02 01 01 63 39 04 00 0a 01 00 0a 01 00 02 01 00 02 01 00 01 01 00 87 0b "objectclass" 30 19 04 17 "supportedSASLMechanisms"` (Surl's `0x3E`). First binds (msgid 2):
  - PLAIN (also `--anyauth`): `30 19 02 01 02 60 14 02 01 03 04 00 a3 0d 04 05 "PLAIN" 04 04 00 75 00 70`; with `--sasl-authzid z` `04 05 7a 00 75 00 70`.
  - LOGIN: `30 16 .. 04 05 "LOGIN" 04 01 75`, then msgid 3 `04 01 70`.
  - CRAM-MD5 (picked by `AUTH=*`): `30 16 .. a3 0a 04 08 "CRAM-MD5"`, no credentials; challenge `<1.2@x>` answered `u c13529b374cb7dfa454265a180a98470`. No serverSaslCreds: exit 8.
  - DIGEST-MD5: `30 18 02 01 02 60 13 02 01 03 04 00 a3 0c 04 0a "DIGEST-MD5"`, no credentials.
  - NTLM: `30 34 .. 04 04 "NTLM" 04 20 4e544c4d53535000 01000000 06820800` + 16 zero bytes; a junk challenge: cancel `30 0e 02 01 03 60 09 02 01 03 04 00 a3 02 04 00`, unbind, 67 `Authentication cancelled`.
  - OAUTHBEARER (`--oauth2-bearer tok -u u:`): `30 4f .. "n,a=u,\1host=172.26.96.1\1port=22394\1auth=Bearer tok\1\1"`; error challenge answered `04 01 01`, then 49: 67.
  - EXTERNAL: `-u u:` `30 19 .. 04 08 "EXTERNAL" 04 01 75`; `-u :` or no `-u` `30 18 .. 04 00`; refused: 67.
  - Exit 67 `Login denied` with no bind (search, unbind): GSSAPI for a user naming no realm, bearer or `--anyauth` without `-u`, no allowed mechanism offered. Success before the last answer (LOGIN, CRAM-MD5): 67. GSSAPI for `u@R` with no ticket: 94.
- Not measured: a GSSAPI bind with a ticket (no KDC; its token differs per ticket). The test pins its framing around a scripted token.
- Fast tests green (the one failure of the last full run was the new DIGEST-MD5 pin, fixed; LDAP tests 919 passed after). `Measure-CodeQuality.ps1` for Ldap and Engine: Engine passed; Ldap 100% line and branch, 0 failing after the fix. Tests are platform-neutral (OpenLDAP dialect injected).

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 26.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 7e74135a ca39b51d 3d836d5e.
- 2026-11-24: Doing -> Done. OpenLDAP SASL binds run every curl mechanism with upstream's request bytes and exit codes (ADR-1359)
