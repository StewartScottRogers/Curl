# ADR-1395: Over SMTP a bearer token without a user gives an empty credential, and a withheld token chooses no mechanism

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation
- Task: BL-5963

## Context

ADR-1361 left two SMTP cases. Measured with `Record-CurlExchange.ps1 -Smtp` against curl
8.22.0 (Schannel build), each with `--oauth2-bearer tok`:

- Without `-u`, `--login-options AUTH=PLAIN`, `LOGIN`, `CRAM-MD5` or `*` log in with an empty
  user and password. Without `AUTH=` no password mechanism is chosen (exit 67).
- After an HTTP redirect to `smtp://v@host`, which withholds the token, nothing follows
  `EHLO` (exit 67) whatever is offered; `-v` gives a reason only for `OAUTHBEARER` and
  `XOAUTH2`, otherwise `no overlap`. With `AUTH=PLAIN` or `AUTH=*` curl logs in as `v`.

## Decision

`SmtpSaslAuthentication` follows the measurements:

- With a bearer token and no credential, the SASL request carries an empty user and password.
- With `MailRequestOptions.BearerTokenWithheld` and no `AUTH=` option, no mechanism is
  chosen, and only the bearer mechanisms are given a `-v` reason.

The rule lives in the SMTP handler, which already holds the transfer's mail options, so
`ISaslAuthenticator` and `SaslRequest` do not change.

## Consequences

`AUTH=DIGEST-MD5` with an empty user, and `-u :` the same way, sends nothing after the
challenge in curl's SSPI build (exit 67). That rule is not SMTP's:
`Curl.Authentication.UnitLibrary` applies it (BL-5968, ADR-1398).
