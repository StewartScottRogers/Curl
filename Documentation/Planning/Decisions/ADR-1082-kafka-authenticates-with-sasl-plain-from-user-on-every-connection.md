# ADR-1082: Kafka authenticates with SASL PLAIN from --user on every connection

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4276)
- Supersedes the "SASL authentication (`--user`)" item of ADR-1076's "Left out"

## Context

ADR-1076 offered no SASL, so a broker whose listener demands it closed the connection at the first request. Kafka's
SASL is SaslHandshake (API key 17) naming a mechanism, then SaslAuthenticate (API key 36) carrying the mechanism's
bytes. PLAIN (RFC 4616) is the one mechanism `--user user:password` maps onto without a new option, as STOMP's
`--user` maps onto `login` and `passcode`. curl has no Kafka support, so there is no curl output to measure; the
extension is tested against the protocol guide.

## Decision

- With `--user`, every connection the transfer opens (the URL's broker and, when ADR-1080 reconnects, the partition's
  leader) sends SaslHandshake v1 naming `PLAIN`, then SaslAuthenticate v0 whose `auth_bytes` are RFC 4616's message
  in UTF-8: an empty authorization identity, NUL, the user name, NUL, the password. Only then does it send Metadata.
  Both versions are the last before flexible versions and are still accepted by Kafka 4.0, matching the request
  header v1 the other requests use.
- Without `--user`, nothing changes: no handshake is sent.
- A SaslHandshake error (UNSUPPORTED_SASL_MECHANISM, 33) ends with exit 67 (CURLE_LOGIN_DENIED) and
  `Kafka broker does not enable SASL PLAIN, it enables: <mechanisms>`, since the broker refuses the credentials'
  mechanism.
- A SaslAuthenticate error (SASL_AUTHENTICATION_FAILED, 58, or any other) ends with exit 67 and
  `Kafka SASL PLAIN authentication failed: <the broker's error_message>`, or `error <code>` when it gives none. Exit
  67 is what curl gives every protocol whose server refuses the login.
- A malformed or wrongly correlated SASL response ends with exit 8, and a failed send or read with 55 or 56, as for
  every other Kafka request.

## Left out, and why

- SCRAM-SHA-256, SCRAM-SHA-512, GSSAPI and OAUTHBEARER: each needs its own exchange and, for the last two, options curl
  maps to other protocols' mechanisms (`--negotiate`, `--oauth2-bearer`). A later task can add them
  when one is wanted.
- An authorization identity: `--sasl-authzid` is not read; the identity is always empty, which brokers treat as the
  authentication identity.

## Consequences

`kafka://` and `kafkas://` reach SASL_PLAINTEXT and SASL_SSL listeners with PLAIN. The client code is
`KafkaClientFlow.AuthenticateAsync` and the wire code `Curl.Extension.Kafka.UnitLibrary/Wire/KafkaSasl`.
