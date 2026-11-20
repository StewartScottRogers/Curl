# ADR-1317: Kafka logs in with SCRAM-SHA-512 or SCRAM-SHA-256 when the broker enables no PLAIN

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5600, Surly report 2026-11-19T1942 finding 5). Builds on ADR-1082.

## Context

ADR-1082 logs in with SASL PLAIN from `-u`. Kafka brokers commonly enable only SCRAM-SHA-256 or SCRAM-SHA-512 (KIP-84). Upstream curl has no Kafka, so this is an extension, tested against RFC 5802, RFC 7677 and the Kafka protocol guide. The transfer context carries no `--login-options`, and the SaslHandshake response lists the broker's mechanisms only when it refuses the one named.

## Decision

- With `-u` the client names PLAIN first, as before, so PLAIN brokers see no change.
- When the handshake is refused with UNSUPPORTED_SASL_MECHANISM and the broker's list holds SCRAM-SHA-512 or SCRAM-SHA-256, the client opens a new connection (the broker closes the old one) and names the strongest offered, SHA-512 before SHA-256. It remembers the choice for the leader's connection of the same transfer.
- SCRAM runs as SaslAuthenticate v0 messages: client-first (`n,,n=user,r=nonce`), client-final (`c=biws,r=...,p=proof`) and the check of the broker's `v=` signature, built on `Rfc2898DeriveBytes.Pbkdf2`, HMAC and the hash of the BCL. The client nonce is 18 random bytes in base64. The user name's `=` and `,` are escaped; the password is sent as UTF-8 without SASLprep.
- A refused login (SaslAuthenticate error, for a wrong password error 58) ends with exit 67 `Kafka SASL <mechanism> authentication failed: <broker message>`. A malformed server message or a wrong server signature ends with exit 8. A broker that offers neither PLAIN nor SCRAM ends with exit 67 listing what it offers.
- The password is only hashed into the proof; it is in no message, `-v` output or `--log-file` text.

## Consequences

A SCRAM-only broker costs one refused connection before the login. Choosing by `--login-options AUTH=` is left out: the option is not carried to extensions, and the offered-order choice needs no flag.
