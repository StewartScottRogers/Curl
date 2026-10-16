# ADR-0574 — `Libcurl`'s authentication option setters check values as curl 8.22.0's `setopt.c` does

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2404.

## Context

BL-2404 adds a setter row (ADR-0560) for every `CURLOPT_*` that `EasyAuthenticationOptions` holds.
The task asks that an unknown `CURLAUTH_*` bit answer `BadFunctionArgument`. Upstream's `httpauth()`
does not refuse unknown bits below `CURLAUTH_ONLY`: it keeps them, and it answers `CURLE_NOT_BUILT_IN`
only when no method bit below `CURLAUTH_ONLY` is left.

## Decision

1. The rows live in `Libcurl/Options/Authentication`. `EasyOptionSetters.AddAuthenticationSetters`
   calls `EasyAuthenticationOptionSetters.AddTo`, with one named static method per row, as ADR-0571 does.
2. `HTTPAUTH` and `PROXYAUTH` map the bitmask onto `HttpAuthenticationMethods`, which carries 32 bits.
   A value with a bit the type cannot hold is `BadFunctionArgument`: anything outside
   `int.MinValue..uint.MaxValue`. A sign-extended 32-bit value fits, so `CURLAUTH_ANY` works both as
   -17 and as 4294967279. Unnamed bits 8 to 30 are kept, as upstream keeps them. A mask that is not 0
   but has no bit below `CURLAUTH_ONLY` (for example `CURLAUTH_ONLY` on its own) is `NotBuiltIn`, as
   upstream answers it. Curl builds every method, so no bit is switched off. `DIGEST_IE` is stored as
   given, and the engine reads it.
3. `NETRC` outside 0..2 is `BadFunctionArgument`. `GSSAPI_DELEGATION` keeps only its two flag bits.
   `TLSAUTH_TYPE` and `PROXY_TLSAUTH_TYPE` take `SRP` in any case, or `null`; any other string is
   `BadFunctionArgument`. Curl builds TLS-SRP, so these options are never `UnknownOption`.
4. `AWS_SIGV4` given a string also sets `HttpAuthentication` to `AwsSigV4`, as upstream does.
   `null` clears only the string.
5. `USERPWD` and `PROXYUSERPWD` are stored whole, in the record's own fields. Upstream splits them
   into a user and a password. That split is left to the engine.
