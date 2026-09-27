# ADR-0028 — The auth scheme is ranked as libcurl ranks it, with no fallback

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-218 chooses which scheme answers a `401` or `407` when the server offers several and
`--basic`, `--digest` or `--anyauth` allows several. BL-216 and BL-217 built the answers
(`BasicAndBearerAuthenticator`, `DigestAuthenticator`); NTLM and Negotiate are not built,
because they need state across a connection's handshake legs (ADR-0014).

libcurl picks one scheme from those both offered and allowed, in the order Negotiate,
Bearer, Digest, NTLM, Basic (`pickoneauth` in `lib/http.c`). The reference build (mingw
curl 8.21.0, ADR-0018) has NTLM and SPNEGO. Measured on 2026-09-26 against a loopback
server answering every request with `401` (commands and bytes in BL-218's Notes):

| Offered | Allowed | Reference curl |
| --- | --- | --- |
| Basic, Digest (two headers, or one) | `--anyauth`, `--basic --digest` | answers Digest |
| Basic, NTLM | `--anyauth` | answers NTLM, never Basic |
| Negotiate, Basic | `--anyauth` | retries with no `Authorization`, never Basic |
| Negotiate, Digest | `--anyauth` | retries with no `Authorization`, never Digest |
| Digest without a nonce, Basic | `--anyauth` | no retry, exit 94, never Basic |
| Basic | `--digest` | no retry, exit 0 |
| Digest | `--basic` | Basic sent up front, no retry, exit 0 |
| `Foo` | `--anyauth` | no retry, exit 0 |

## Decision

- `RankedHttpAuthenticator` picks the scheme exactly as libcurl does, treating NTLM and
  Negotiate as offered when the server offers them, as the reference build does. It hands
  a pick of Digest to `DigestAuthenticator` and everything else to
  `BasicAndBearerAuthenticator`.
- There is no fallback. When the pick is NTLM or Negotiate, or a Digest challenge curl
  cannot read, it sends nothing, rather than answer a lower-ranked scheme the reference
  build would never have answered.
- The ranking lives once, in `HttpAuthSchemeRanking`, shared by both authenticators.

## Consequences

- Wherever Curl answers, it answers the scheme the reference curl answers.
- Where the reference curl answers NTLM, Curl sends nothing until NTLM is built. What the
  HTTP handler does after a `null` answer (retry once without a header as curl does for a
  failed Negotiate, or stop with exit 94 for an unreadable Digest challenge) is the
  handler's, not the authenticator's.

## Alternatives considered

- **Rank as a build without NTLM and SPNEGO would, falling back to Digest or Basic.**
  Rejected: that answers servers the Windows reference build does not, and the
  platform's curl is the rule (ADR-0009, ADR-0018).
