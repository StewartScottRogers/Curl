# ADR-0470: SASL PLAIN keeps an empty authorization identity, as curl 8.21.0 sends it

- Status: Accepted
- Date: 2026-10-10
- Task: BL-1992 (gap finding GF-0062)
- Decided by Claude under Stewart's delegation

## Context

Gap finding GF-0062 reports that upstream test938 (two `smtp://` URLs joined by `-:`, with
`-u user.one:secret` and then `-u user.two:secret`) expects the PLAIN message
`user.one NUL user.one NUL secret`, while Curl sends `NUL user.one NUL secret`, and suggests
filling the authorization identity with the user name.

Measured before deciding:

- Real curl 8.21.0 (the Schannel build on Windows), through `Record-CurlExchange.ps1 -Smtp`
  with a server advertising only `AUTH PLAIN`, and `-u user.one:secret`, sends `AUTH PLAIN`,
  then `AHVzZXIub25lAHNlY3JldA==` after the empty `334`: `NUL user.one NUL secret`. Curl sends
  the same bytes.
- curl 8.21.0's `lib/vauth/cleartext.c` builds the message from `Curl_creds_sasl_authzid`,
  which is empty unless `--sasl-authzid` is given; upstream tests 833 and others expect
  `%b64[%00user%00secret]b64%`.
- curl 8.21.0 lists `938` in `tests/data/DISABLED`: upstream's own suite does not run the case,
  whose `<protocol>` still holds the message older curl sent.

## Decision

1. Curl's PLAIN message stays `authzid NUL user NUL password` with an empty authorization
   identity unless `--sasl-authzid` is given, as `SaslAuthenticator` builds it today and
   `SaslAuthenticatorTests.Begin_Plain_InitialResponseMatchesCurl` pins it. Curl does not
   copy the user name into it, for test938 or any other case.
2. A case curl's own `tests/data/DISABLED` lists is not a yardstick for Curl. The gap harness
   measures such cases as `excluded` (BL-2027, interactive only, since the harness is an
   audit path), which is how GF-0062 closes.

## Consequences

- No Curl code changes for GF-0062; Curl stays a drop-in replacement for curl 8.21.0, which a
  change to match test938 would have broken for every SMTP, IMAP and POP3 PLAIN login.
- GF-0062 stays open until BL-2027 makes the harness measure test938 as `excluded`.
