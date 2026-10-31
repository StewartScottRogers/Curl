# ADR-1077: XMPP logs in with SCRAM-SHA-256 or SCRAM-SHA-1 before PLAIN

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4271)

## Context

ADR-1068 left SCRAM-SHA-1 (RFC 5802) and SCRAM-SHA-256 (RFC 7677) undone in the XMPP extension: with `--user` it
logged in with SASL PLAIN only. Many servers offer SCRAM, and some offer it without PLAIN.

## Decision

- SCRAM is now done. Each SASL mechanism is one file in `Curl.Extended.Protocol.Xmpp.UnitLibrary/Client` that adds itself to
  `XmppSaslMechanisms` from a `[ModuleInitializer]` (no central switch, no reflection): `XmppSaslPlain`,
  `XmppSaslAnonymous`, `XmppScramSha1` and `XmppScramSha256`, the last two over the shared `XmppScramExchange`.
- With `--user` the client takes the first mechanism the server offers of SCRAM-SHA-256, SCRAM-SHA-1, PLAIN; without
  it, ANONYMOUS. When none is offered the transfer ends with exit 67 and `XMPP server does not offer SASL
  SCRAM-SHA-256 or SCRAM-SHA-1 or PLAIN` (or `... ANONYMOUS`).
- SCRAM runs without channel binding (GS2 header `n,,`, so `c=biws`), with an 18-byte random client nonce, base64
  encoded. The server-first message must carry a nonce that extends the client's, a base64 salt and a positive
  iteration count. The server signature is accepted in `<success/>`'s additional data (RFC 6120 6.3.10) or in a
  second `<challenge/>`, answered with an empty `<response/>`; it is compared in fixed time.
- A wrong or missing server signature, a malformed server-first message or a server `e=` error ends with exit 67 and
  `XMPP authentication failed: <condition>` (`server signature mismatch`, `malformed SCRAM server-first-message`, or
  the server's error value), the extension's authentication-failure error from ADR-1068. A SASL element whose text is
  not base64 is malformed XML (exit 8).
- The password is used as UTF-8 without SASLprep (RFC 4013): the base class library has no stringprep, ASCII passwords
  are unaffected, and the extension is tested against the RFCs' examples, whose passwords are ASCII.

## Consequences

- Only the base class library is used: `Rfc2898DeriveBytes.Pbkdf2` for `Hi()`, `CryptographicOperations.HmacData` and
  `HashData`, and `CryptographicOperations.FixedTimeEquals`.
- A new mechanism is one new file; the flow (`XmppClientFlow`) carries any number of challenge rounds.
