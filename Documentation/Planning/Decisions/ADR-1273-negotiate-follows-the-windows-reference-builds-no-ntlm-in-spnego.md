# ADR-1273 — --negotiate follows the Windows reference build: no NTLM inside SPNEGO

Status: Accepted. Decided by Claude under Stewart's delegation. Confirms ADR-0176.

## Context

BL-5282, from Surl's Phase 7 measurement: against a host with no Kerberos realm, `--negotiate -u tester:secret`
gets from curl 8.21.0 as tagged (static OpenSSL build) an NTLMSSP-in-SPNEGO login, while Curl stops at the
first step with `SEC_E_NO_CREDENTIALS`, as the Windows (Schannel) reference build does. The reference build
carries upstream's later `!ntlm` change to SPNEGO's package string, so SSPI no longer offers NTLM inside
Negotiate. ADR-0176 pinned that behaviour, measured on the platform's curl.

## Decision

Curl keeps following the platform's curl, the Windows reference build, which carries upstream's later
`!ntlm` change to the Negotiate package: `SspiNegotiateSecurityContext` answers `NoCredentials` when the
first SPNEGO token carries NTLMSSP and sends nothing. A server wanting NTLM is reached with `--ntlm`.
Without `-f` a 401 is then the answer, as curl's is; with `-f` the exit is 22 with the SSPI failure line.

## Consequences

The 8.21.0-as-tagged behaviour (NTLM inside SPNEGO) is not reproduced. If the platform's curl ever drops
the `!ntlm` change, this ADR and `SspiNegotiateSecurityContext` change together.
