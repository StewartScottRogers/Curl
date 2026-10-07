---
id: BL-692
title: Encode and decode SPNEGO tokens for the Negotiate scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions/ADR-0167-the-hand-built-spnego-offers-kerberos-v5-alone-as-mit-does-and-reads-negtokenresp-with-typed-failures.md, Documentation/Planning/Decisions/ADR-0142-ntlm-negotiate-and-kerberos-answer-through-sspi-on-windows-and-curls-own-code-or-the-system-gss-api-elsewhere.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-692 — Encode and decode SPNEGO tokens for the Negotiate scheme

## Goal

`Curl.Authentication.UnitLibrary` encodes SPNEGO NegTokenInit (with the mechanism list curl's build offers: Kerberos V5, the legacy Microsoft Kerberos OID, NTLMSSP, in its order) and decodes NegTokenResp (negState, supportedMech, responseToken, mechListMIC), per RFC 4178, with `System.Formats.Asn1`, so the Negotiate authenticator (BL-527) can carry either a Kerberos (BL-691) or an NTLM (BL-683) token.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): Negotiate works on every platform. Design and seam: BL-525's ADR. SPNEGO is the Negotiate scheme's own wrapping (RFC 4559 for HTTP), so it lives with the Negotiate authenticator rather than in the Kerberos or NTLM library.
- RFC 4178 section 4.2 (NegotiationToken), RFC 2743 section 3.1 (the initial-token framing with OID `1.3.6.1.5.5.2`). The mechanism list and order: record them from a real curl `--negotiate` exchange (Windows SSPI and Linux GSS-API) through `Record-CurlExchange.ps1` with a `401 Negotiate` server, into Notes.
- Pure code; a malformed token is a typed failure.

## Acceptance criteria

- [x] Measured first as above; the NegTokenInit bytes curl sent copied into Notes per platform.
- [x] `Curl.Authentication.UnitTests` pin the NegTokenInit bytes for a fixed inner token and each mechanism list, decode accept-completed, accept-incomplete and reject NegTokenResp examples, and reject malformed tokens with the typed failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured 2026-09-28

- **Windows**, curl 8.21.0 (Schannel, SSPI), `Record-CurlExchange.ps1` answering
  `401` + `WWW-Authenticate: Negotiate`, with `-u :`, `-u u:p`, `-u EXAMPLE\u:p` and
  `-u u@EXAMPLE.COM:p`: no `Authorization` header at all, one request, exit 0. Outside a
  domain SSPI's Negotiate yields nothing, so there are no Windows NegTokenInit bytes to
  copy. Windows Negotiate is SSPI's anyway (ADR-0142), never the hand-built SPNEGO.
- **Ubuntu (WSL)**, curl 8.18.0, mit-krb5 1.22.1, no `gss-ntlmssp`, no `kinit` or sudo:
  a throwaway `dotnet run` C# app wrote a `FILE:` v4 cache holding one service ticket for
  `HTTP/localhost@TEST.ORG` (dummy aes256 ticket, `krb5.conf` with `default_realm =
  TEST.ORG`, `dns_canonicalize_hostname = false`); MIT then builds the AP-REQ with no KDC.
  `Record-CurlExchange.ps1` could not be reached from WSL (the Windows listener timed
  out, exit 28), so the server was `nc -l` inside WSL and the header was read from
  `curl -v`. `KRB5CCNAME=FILE:/tmp/bl692cc curl --negotiate -u : http://127.0.0.1:18692/`
  sent:

  ```
  Authorization: Negotiate YIIBpwYGKwYBBQUCoIIBmzCCAZegDTALBgkqhkiG9xIBAgKiggGEBIIBgGCCAXwGCSqGSIb3EgECAgEAboIBazCCAWegAwIBBaEDAgEOogcDBQAgAAAAo4GHYYGEMIGBoAMCAQWhChsIVEVTVC5PUkeiHDAaoAMCAQOhEzARGwRIVFRQGwlsb2NhbGhvc3SjUDBOoAMCARKhAwIBAaJCBEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAApIHHMIHEoAMCARKigbwEgbl11iRFt/nyxx5DWYBenynZq+UP8yMG1p1LWE1m4GlKoHo6q7lg8Vyw6sD16g9W2XQ9xHik/hsh6/Z3YW7z+X/30b1fteHCDJx4pvd9akGBOtNaU0QmDIYqffgs4T8iAJskRFNIFofxrtiKWVuGkn8bqPvKwrb7SxmCd56CaFsJLlibVls9puDHK5Mdifot53QWqJiQY0+LRtGDVHTIXXpFuXvfH9toQ4MP1a6NrmmKS1eIUF97JsFskA==
  ```

  Decoded: `60 82 01 A7`, `thisMech` `1.3.6.1.5.5.2`, `A0` NegTokenInit whose
  `mechTypes` holds **only** `1.2.840.113554.1.2.2` (Kerberos V5), then `mechToken` (the
  384-byte AP-REQ, from offset 43). No `reqFlags`, no `mechListMIC`.

### Decisions

- The mechanism list is Kerberos V5 alone, not the task's "Kerberos V5, legacy Microsoft
  OID, NTLMSSP": that is what MIT's curl sent, and SSPI writes Windows' token itself.
  The encoder takes the list as a parameter, and tests pin the measured list, the
  three-mechanism SSPI-style list and NTLMSSP alone. ADR-0167; ADR-0142's guessed list
  corrected to point at it.
- NegTokenResp is read unframed (`[1]` choice) in BER; anything else is
  `UnexpectedToken`, every structural fault `Malformed` (`SpnegoTokenException`). An
  `Encode` was added to the record for the initiator's later tokens (NTLM Type 3 inside
  SPNEGO); it is the same structure, and round-trip tests cover it.
- Types are `internal`: only the Negotiate adapter in this library (BL-527) uses them.
- `touches` widened to ADR-0167 (new), ADR-0142 (its mechanism-list sentence was wrong)
  and the Decisions README index. No task in `Doing` names any of them.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. SPNEGO NegTokenInit encodes curl's measured MIT token byte for byte and NegTokenResp decodes with typed failures
