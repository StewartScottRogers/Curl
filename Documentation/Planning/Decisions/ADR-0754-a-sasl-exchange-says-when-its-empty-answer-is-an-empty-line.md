# ADR-0754 — A SASL exchange says when its empty answer is an empty line

- Status: Accepted
- Date: 2026-10-21
- Task: BL-3004
- Decided by Claude under Stewart's delegation.

## Context

curl 8.21.0's `build_message` (`lib/curl_sasl.c`) tells two empty answers apart: a mechanism
step that sets no message at all is sent as an empty line, and one that sets a message of zero
bytes is sent as `=`, the explicit empty response. DIGEST-MD5 answers the server's `rspauth`
challenge with no message (upstream test823 for IMAP, test869 for POP3, and the SMTP exchange
`SaslAuthenticatorTests` records); `LOGIN` with an empty password sends `=`. Curl's
`ISaslExchange.RespondAsync` returns raw bytes, so both arrived at the IMAP, POP3 and SMTP
handlers as an empty array and both were sent as `=`.

## Decision

1. `ISaslExchange` gains a default member, `SendsLastAnswerAsEmptyLine`, `false` unless an
   exchange overrides it. A handler reads it only for an empty answer to a challenge: `true`
   sends an empty line, `false` sends `=` as before.
2. `ChallengeSaslExchange` (CRAM-MD5 and DIGEST-MD5) reports `true`: neither mechanism ever
   sends an explicit empty response, so its only empty answer, DIGEST-MD5's to `rspauth`, is
   curl's "no message".
3. The IMAP, POP3 and SMTP handlers each send the answer through `EncodeAnswer`, which applies
   this; initial responses keep their own rules.

## Consequences

- Upstream cases 823 and 869 pass and join the ratchet; 907 still differs on quote escaping
  in the user name and realm (BL-3005).
- A default interface member keeps every existing `ISaslExchange` (the other mechanisms and
  the test fakes) unchanged; the null-for-cancel meaning of `RespondAsync` stays as it is.
