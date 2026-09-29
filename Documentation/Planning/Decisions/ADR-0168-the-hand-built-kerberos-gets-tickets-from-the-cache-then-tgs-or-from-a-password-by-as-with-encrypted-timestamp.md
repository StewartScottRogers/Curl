# ADR-0168 — The hand-built Kerberos gets tickets from the cache then TGS, or from a password by AS with encrypted timestamp

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-690.

## Context

The hand-built Kerberos client (ADR-0142's fallback route) needs a service ticket for
`HTTP/host@REALM` and the other services curl authenticates to. RFC 4120 gives the
exchanges (AS, section 3.1; TGS, section 3.3) and the transport (section 7.2), and leaves
the client's choices open: which options and lifetime to ask for, which encryption types
to offer, how to pick the pre-authentication key, when to use UDP or TCP, and how to
treat each KDC's failure. MIT Kerberos, the library curl's GSS-API build uses off
Windows, is the reference; SSPI's explicit-credential path is the reference for a
password.

## Decision

- **Two entry points, one per credential source.** `KerberosKdcClient.GetServiceTicketAsync`
  takes either a `CredentialCache` or a `KerberosPasswordCredential`, never both:
  - *Cache:* the cache's own live ticket for the server when it holds one (client equal
    to the cache's default principal, server equal by realm and components, end time
    after now plus the cache's KDC time offset), copied out so the cache and the result
    are disposed independently; otherwise a TGS exchange with the live
    `krbtgt/REALM@REALM` of the default principal's realm; otherwise
    `KerberosKdcError.NoCredentials`. This is what MIT's `gss_init_sec_context` does.
  - *Password:* an AS exchange for `krbtgt/REALM@REALM`, then a TGS exchange, as SSPI
    does with explicit credentials. The cache is not consulted.
- **AS exchange as MIT's `kinit` runs it.** The first AS-REQ carries no pre-authentication.
  On `KDC_ERR_PREAUTH_REQUIRED` the second carries `PA-ENC-TIMESTAMP` (key usage 1) in the
  first encryption type of the error's `PA-ETYPE-INFO2` this library has, with that
  entry's salt and string-to-key parameters; with no `PA-ETYPE-INFO2`, the first type
  offered and the default salt (realm then name components). The AS-REP is decrypted
  (key usage 3) in the key for the reply's own encryption type, salted by the reply's
  `PA-ETYPE-INFO2`, else the error's, else the default. Options: none; till: now plus one
  day (MIT's default `ticket_lifetime`); no renewal asked for.
- **TGS exchange with a plain authenticator.** `PA-TGS-REQ` carries an AP-REQ with the
  ticket-granting ticket and an authenticator (key usage 7) whose checksum is the
  session key type's keyed checksum of the encoded `KDC-REQ-BODY` (key usage 6); no
  subkey, so the TGS-REP is read in the session key (key usage 8). Options: none; till:
  the ticket-granting ticket's end time, as MIT asks. The request goes to the KDCs of the
  realm the ticket-granting ticket is for (the last component of `krbtgt/REALM`).
- **Offered encryption types:** `aes256-cts-hmac-sha1-96`, `aes128-cts-hmac-sha1-96`,
  `aes256-cts-hmac-sha384-192`, `aes128-cts-hmac-sha256-128`, `rc4-hmac`: MIT's default
  order of the types BL-686 built. A reply, a key hint or a cached session key in any
  other type is `EncryptionTypeNotSupported`. Honouring `permitted_enctypes` and
  `default_tkt_enctypes` needs MIT's enctype-name parser and is left to its own task.
- **Every reply is checked.** A KRB-ERROR becomes `KerberosKdcException` with its code and
  text and, for the codes curl's users meet, a named `KerberosKdcError`
  (`ClientPrincipalUnknown`, `ServerPrincipalUnknown`, `PreAuthenticationFailed`,
  `ClockSkew` and the rest); any other code is `KdcRefused`. An AS-REP that does not
  decrypt is `ReplyIntegrityCheckFailed` (a wrong password when the KDC does not ask for
  pre-authentication). A reply that does not decode, is the wrong message, repeats
  another nonce or names another server is `UnexpectedReply`. The time checks are the
  KDC's (`KRB_AP_ERR_SKEW`); the client does not second-guess the reply's times.
- **Transport as RFC 4120 section 7.2 and MIT.** `KerberosKdcSender` tries each KDC the
  locator returns, in order, and moves on when one throws `IOException`; none answering is
  `KdcUnreachable`, none located is `NoKdc`. A `udp/` KDC gets UDP, a `tcp/` KDC TCP, and a
  plain one UDP when the request is at most `udp_preference_limit` bytes (MIT's rule).
  A UDP reply of `KRB_ERR_RESPONSE_TOO_BIG` (52) is asked again of the same KDC over TCP,
  as Windows does. TCP messages carry a four-byte big-endian length; a reply claiming more
  than 1 MiB is `UnexpectedReply` rather than an allocation of whatever the peer says.
  `https://` (MS-KKDCP) KDCs are skipped until a KDC proxy client is built.
- **Nonces are 31 bits** from the injected random source, as MIT sends, because some KDCs
  read the field as signed. Timestamps come from the injected `TimeProvider`, split into
  whole seconds and microseconds.

## Consequences

- `IKerberosKdcTransport` moves bytes only; `Curl.Networking.UnitLibrary` supplies the
  socket implementation and BL-527 composes it.
- Cross-realm referrals are not followed: a TGS-REP naming a server other than the one
  asked for is refused. MS-KKDCP, enctype names from `krb5.conf` and writing new tickets
  back to the cache are separate work.
- Tests drive every path through `FakeKdc` in `Curl.Kerberos.UnitTests`, an in-memory
  KDC built from this library's own messages and encryption with fixed keys.
