# ADR-0188 — Service names and delegation reach every security context, and SSPI never delegates

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-631.

## Context

BL-630 parses `--service-name`, `--proxy-service-name` and `--delegation` into
`CommandLineOptions` (`ServiceName`, `ProxyServiceName`, `GssApiDelegation`). ADR-0142 said the
two names set the target name and `--delegation` maps to
`AllowedImpersonationLevel = Delegation` for the system route on every platform. Until BL-631
nothing applied them: `NegotiateHttpAuthenticator` always asked for `HTTP` and no delegation.

What curl 8.21.0 does, from its source (`lib/http_negotiate.c`, `lib/vauth/spnego_gssapi.c`,
`spnego_sspi.c`, `krb5_sspi.c`, `lib/curl_gssapi.c`); none of it can be measured on the loopback
recorder, which has no KDC:

- HTTP Negotiate names the service `--proxy-service-name` for a proxy and `--service-name` for
  the server, each `HTTP` when not given; the other peer's option never applies. The GSS-API
  build joins it as `service@host` (a host-based service name), SSPI as `service/host`.
- SASL (`smtp`, `pop`, `imap`) takes `--service-name` in place of the scheme's service.
- `--delegation` (`CURLOPT_GSSAPI_DELEGATION`) is read only by `curl_gssapi.c`, which adds
  `GSS_C_DELEG_FLAG` for `always` and `GSS_C_DELEG_POLICY_FLAG` for `policy`. The SSPI code
  never reads it: the Schannel build asks no delegation whatever the option says.

## Decision

1. `Curl.Authentication`'s `NegotiateOptions` (the two names and a `SecurityDelegation`) is given
   to `NegotiateHttpAuthenticator`, which asks for `ProxyServiceName ?? "HTTP"` when the request
   is for a proxy, `ServiceName ?? "HTTP"` otherwise, and passes the delegation level on in
   `SecurityContextRequest.Delegation`. `Curl.Console`'s `NegotiateOptionsMapping` builds it from
   the option group, for the origin's handler and for the CONNECT tunnel's authenticator.
2. `MailRequestOptions.ServiceName` is `--service-name`, so SASL GSSAPI and NTLM use it once
   `Curl.Console` gives the SASL authenticator a security-context factory (BL-852).
3. On Windows `RoutingSecurityContextFactory` clears the delegation level before it reaches SSPI,
   as the Schannel build never asks one. This replaces ADR-0142's "for W" in its delegation line.
4. Off Windows the system GSS-API route (`SystemSecurityContextFactory`) asks
   `TokenImpersonationLevel.Delegation` (`GSS_C_DELEG_FLAG`) for `always` only.
   `NegotiateAuthentication` has no way to ask `GSS_C_DELEG_POLICY_FLAG`, and asking full
   delegation for `policy` would forward the user's ticket-granting ticket to hosts the realm
   has not marked ok-as-delegate, which `policy` exists to prevent; so `policy` asks none there.
5. The hand-built Kerberos route (K) delegates as MIT does (BL-873): for `always`, and for
   `policy` with an ok-as-delegate service ticket, `KerberosServiceTicketSource` gets a
   forwarded ticket-granting ticket (ADR-0210) and the initial token carries the delegation
   flag and a `KRB-CRED`; a ticket-granting ticket that is not forwardable, or any failure to
   forward it, sends no delegation, as MIT drops the flag when `krb5_fwd_tgt_creds` fails.
   SASL passes no level yet (BL-874).

## Consequences

- `--service-name` and `--delegation always` change what the server sees wherever a context is
  made; `--proxy-service-name` takes effect as soon as proxy Negotiate is answered (BL-604).
- `--delegation policy` on the system GSS-API route delegates less than curl's GSS-API build
  when the service ticket is ok-as-delegate. Only the hand-built route can close that gap.
- SOCKS5 GSS-API (BL-615) takes `--proxy-service-name` the same way when it lands.

## Alternatives considered

- **Map `policy` to full delegation on the system route.** Rejected: it hands the credential to
  hosts the realm says must not get it.
- **Honour `--delegation` through SSPI on Windows (ADR-0142 as written).** Rejected: the Schannel
  build does not, and a drop-in replacement must not delegate where curl does not.
- **Add the names to `HttpAuthRequest`.** Rejected: they are per option group, not per request,
  and the authenticator is already built per group; it would widen a shared contract for nothing.
