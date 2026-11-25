# ADR-1299: The OpenLDAP dialect binds with SASL when the auth mask or AUTH= login options ask (BL-5435)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context

Fitness finding STD-117: on the OpenLDAP dialect `ChooseBindKind` always chose a simple bind, so `--digest`, `--ntlm`, `--negotiate` and `--login-options AUTH=...` sent the password in clear.

## Decision

With credentials, when `--login-options` holds an `AUTH=` option or the auth mask lacks Basic, the OpenLDAP dialect reads `supportedSASLMechanisms` from the rootDSE and binds with SASL DIGEST-MD5 if the server offers it and `AUTH=DIGEST-MD5`/`AUTH=*` (or, with no `AUTH=`, the Digest flag) allows it. Otherwise it unbinds and fails with exit 67 `Login denied`. The password never crosses the wire in clear. Default (Basic) transfers keep the simple bind. NTLM and GSS-SPNEGO are not offered by OpenLDAP servers in the finding, so they end in exit 67.

## Consequences

The measured reference (curl 8.18.0 OpenLDAP) is matched for the finding's cases; the mechanism choice among several offered mechanisms is limited to DIGEST-MD5.
