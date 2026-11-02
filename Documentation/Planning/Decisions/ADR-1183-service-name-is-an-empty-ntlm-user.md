# ADR-1183: --service-name without -u is an empty NTLM user

- Status: Accepted
- Date: 2026-11-02
- Amends ADR-1182
- Decided by Claude under Stewart's delegation (BL-4460, AF-0524)

## Context

The audit office found (AF-0524) that curl 8.22.0 (Schannel) given `--ntlm --service-name x` and no `-u`
sends `Authorization: NTLM TlRMTVNTUAAB...` (the Type 1 message) on its first request, `-v` logging
`* Server auth using NTLM with user ''`; with `-d x` that request is the empty `Content-Length: 0` probe.
Without `--service-name` neither curl nor Curl sends NTLM. Curl's HTTP handler never saw `--service-name`
(it reached only `MailRequestOptions` and `NegotiateOptions`), so it had no credential and sent no
`Authorization`. ADR-0142 covers the default credentials only for `-u :`.

## Decision

- `HttpRequestOptions.ServiceName` carries `--service-name` to the HTTP handler;
  `HttpRequestOptionsMapping.Of` copies it from `EasyAuthenticationOptions.ServiceName`.
- With no transfer credential, a set service name and NTLM the one scheme allowed, the origin's
  `HttpAuthRequest` gets an empty user name and password (`HttpProtocolHandler.OriginCredentialOf`,
  `StartsWithAnEmptyUser`), the same empty credential ADR-0142 answers `-u :` with, so the Type 1
  message goes on the first request and a POST holds its body back for it.
- Any other scheme set (Basic, Digest, `--anyauth`) is unchanged, as ADR-1182 keeps `--sasl-authzid`
  to Digest alone: only `--ntlm` was measured. A proxy is unchanged: `--service-name` is not a proxy
  option.

## Consequences

The finding's reproduction sends the Type 1 message as curl does. `--anyauth --service-name` and
`--negotiate --service-name` keep their old behaviour until a measurement says otherwise.

## Amendment (BL-4509): Windows only

Decided by Claude under Stewart's delegation. The OpenSSL build was measured on 2026-11-02 with
curl 8.18.0 (x86_64-pc-linux-gnu, OpenSSL/3.5.5, the Ubuntu build in WSL; no 8.22.0 OpenSSL build
was at hand, and curl's HTTP auth code sends a host credential only when a user is set, so the
version gap does not change the answer) against a loopback listener:
`curl --ntlm --service-name x http://127.0.0.1:<port>/` sent `GET /` with `Host`, `User-Agent` and
`Accept` only, and with `-d x` sent the whole POST (`Content-Length: 1`, body `x`) with no
`Authorization` either. The empty NTLM user comes from the Schannel build's SSPI default
credentials, so `StartsWithAnEmptyUser` now applies the `--service-name` rule only when
`OperatingSystem.IsWindows()`; off Windows the request goes with no `Authorization`, as the OpenSSL
curl sends it. The `--sasl-authzid` Digest rule (ADR-1182) is unchanged on every platform. Each
platform's answer is pinned in its own `OSCondition` test in
`HttpProtocolHandlerTests.Authentication.cs`, and `StartsWithAnEmptyUser(options, onWindows)` is
tested for both values on every platform.
