# ADR-0184 — SASL GSSAPI and NTLM run on one security context per exchange

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-538.

## Context

curl 8.21.0 ranks SASL mechanisms EXTERNAL, GSSAPI, DIGEST-MD5, CRAM-MD5, NTLM, OAUTHBEARER,
XOAUTH2, PLAIN, LOGIN (`lib/curl_sasl.c`). `SaslAuthenticator` held GSSAPI's and NTLM's places
but treated both as not offered. ADR-0142 gave the `ISecurityContextFactory` seam and its
router (SSPI on Windows, the system GSS-API or the hand-built Kerberos and curl's own NTLM
elsewhere), and ADR-0183 made SASL exchanges awaited and gave contexts `Wrap`/`Unwrap`.

Measured with curl 8.21.0 (Schannel) against `Record-CurlExchange.ps1 -Smtp` (BL-538 Notes):
NTLM sends Type 1 as the initial response and Type 3 in answer to the Type 2 challenge; its
context names `smtp/<host>`; `-u u:p` with `GSSAPI NTLM PLAIN` offered picks NTLM, while
`-u :` and `-u DOMAIN\u:p` pick GSSAPI. GSSAPI's exchange past the first token needs a KDC and
cannot be measured on the loopback recorder.

## Decision

1. **One context per exchange.** `Begin` asks the factory for one context for the SASL service
   (`smtp`, `imap`, `pop`, or `--service-name`) on the URL's host, with `-u`'s credential split
   as curl splits `DOMAIN\user` (the empty user name asks for the default credentials), and the
   `SecurityContextSaslExchange` keeps it until it has nothing more to send, then disposes it.
   Unlike HTTP's NTLM (ADR-0181), the SASL exchange stays on one connection, so no fresh
   context per leg is needed.
2. **NTLM:** the initial response is the context's first token (Type 1); the one answer is the
   next step's token for the Type 2 challenge (Type 3); any further challenge is not answered.
3. **GSSAPI** runs on `SecurityMechanism.Kerberos`, the raw RFC 4121 mechanism, not SPNEGO
   (RFC 4752), asking `ProtectionLevel.Sign` so SSPI negotiates the keys `Wrap` needs. The
   initial response is the first token; while the context is not established each challenge is
   stepped and its token answered (empty after an AP-REP); once it is established the next
   challenge is the wrapped security-layer offer. It must unwrap to four bytes with the "no
   security layer" bit (`0x01`) set; the answer is `01 00 00 00` followed by `--sasl-authzid`,
   wrapped without encryption, as curl's `Curl_auth_create_gssapi_security_message` does.
   Anything else is not answered. Stepping until the context is established, rather than
   assuming curl's no-mutual-authentication path, handles both an acceptor that sends an
   AP-REP first and one that sends the offer at once.
4. **Ranking** follows curl: GSSAPI needs a user name with `\`, `/` or `@` neither first nor
   last, or an empty one (`Curl_auth_user_contains_domain`); both need a user and no bearer
   token, like the other password mechanisms. Without a factory
   (`new SaslAuthenticator(Encoding)`) both stay not offered and `Begin` refuses them.
5. **A failed step answers `null`**, so the handler cancels with `*` and exit 67 under
   ADR-0183's contract. curl fails with exit 94 and sends nothing more (measured); matching it
   needs the contract to carry the exit, filed as BL-855.

## Consequences

- Both mechanisms are available on every platform through the router: no `OSCondition` refusal.
- `CurlComposition` still builds `SaslAuthenticator` without a factory until BL-852 hands it
  the router.
- A context abandoned mid-exchange (the server drops the connection) is left to its finalizer.
