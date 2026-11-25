# ADR-1398: The SSPI behaviour refuses SASL DIGEST-MD5 with an empty user with exit 67

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation
- Task: BL-5968

## Context

curl's Schannel build gives SSPI no identity for an empty user, so SSPI is asked to use the
logged-on Windows account. Curl answered the challenge with `username=""` and exited 0.

Measured with `Record-CurlExchange.ps1 -Smtp` against curl 8.22.0 (Schannel build), the
server offering `DIGEST-MD5`, with `--login-options AUTH=DIGEST-MD5`:

- `--oauth2-bearer tok` without `-u`, `-u ':'` and `-u ':p'` send `AUTH DIGEST-MD5`, nothing
  after the challenge, and exit 67.
- `-u ':'` with a challenge that has no `nonce` also exits 67, where `-u u:p` exits 94, so
  the empty user is refused before the challenge is read.
- An empty challenge is still cancelled with `*` first (BL-5033).
- With `PLAIN LOGIN` offered beside it and no `--login-options`, curl exits 67 without trying
  another mechanism.

## Decision

Under the SSPI behaviour, `SaslAuthenticator` answers a non-empty DIGEST-MD5 challenge for
an empty user with no answer and no cancel reason, which every mail handler ends as exit 67
with nothing sent. Curl never tries the logged-on account's digest credentials: the
measurement shows curl failing there, and failing is the simplest drop-in behaviour. The
OpenSSL behaviour keeps curl's own DIGEST-MD5 code, which answers an empty user (ADR-0139).

## Consequences

- An account for which Windows would grant digest credentials without a user name was not
  measured; Curl exits 67 there too.
- The rule is in `Curl.Authentication.UnitLibrary`, so the handlers do not change.
