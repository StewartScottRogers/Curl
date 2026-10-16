# ADR-0516 — Mapping the command line's authentication group onto `EasyAuthenticationOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2306.

## Context

ADR-0506 has `Curl.Console/EasyOptionsFromCommandLine.cs` map a parsed command line onto the easy
option groups as the curl tool's `config2setopts.c` does; ADR-0513 to ADR-0515 settled the network,
TLS and proxy groups. BL-2306 adds the authentication group, `EasyAuthenticationOptions`. Each
mapping agrees with the `CURLOPT_*` lines `--libcurl` writes, except where the command line's
behaviour (what the engine does with `CommandLineOptions`) differs; then the behaviour wins.

`CommandLineOptions` keeps the origin's methods as `AuthSchemes` and the proxy's as
`ProxyAuthSchemes`, both in `Curl.Protocol.Abstractions`' `HttpAuthSchemes` (Basic, Digest, NTLM,
Negotiate, Bearer), already resolved as the engine uses them. `--libcurl` instead writes
`CURLOPT_HTTPAUTH` from the raw switches: `--anyauth` as `CURLAUTH_ANY` less each later `--no-`
scheme, plus Bearer for `--oauth2-bearer` and `CURLAUTH_AWS_SIGV4` for `--aws-sigv4`.

`--ntlm-wb`, `--krb`, `--tlsauthtype`, `--tlsuser`, `--tlspassword` and their proxy twins have no
function in the reference build: the parser accepts them and keeps no value.

Curl's HTTP message signature options (`--httpsig-algo`, `--httpsig-key`, `--httpsig-keyid`,
`--httpsig-headers`) have no `CURLOPT_*`.

## Decision

1. `HttpAuthentication` and `ProxyAuthentication` come from `AuthSchemes` and `ProxyAuthSchemes`, the
   sets the engine authenticates with. A set holding every scheme the engine knows (Basic, Digest,
   NTLM and Negotiate, which only `--anyauth`, `--proxy-anyauth` or naming all four gives) is
   `CURLAUTH_ANY`; any other set is each scheme's `CURLAUTH_*` bit. `--aws-sigv4` adds
   `CURLAUTH_AWS_SIGV4` to the origin's methods, as `--libcurl` writes it. Where this differs from
   `--libcurl` (`--anyauth --no-basic` is Digest, NTLM and Negotiate here, `CURLAUTH_ANY` less Basic
   there; four named schemes are `CURLAUTH_ANY` here, their four bits there), the bits the record
   loses or gains are methods the engine never uses for that command line.
2. `-u` and `-U` are `CURLOPT_USERPWD` and `CURLOPT_PROXYUSERPWD` as `user:password`, as `--libcurl`
   writes them; the parser has already asked for a missing password. `CURLOPT_USERNAME`,
   `CURLOPT_PASSWORD` and their proxy twins stay unset, as curl's tool sets none of them.
3. `-n`, `--netrc-file` and `--netrc-optional` give `CURLOPT_NETRC` as `CommandLineOptions.NetrcUse`
   decides it (optional outranks required), and the file as given.
4. `--location-trusted` is `CURLOPT_UNRESTRICTED_AUTH`; `--delegation` `policy` and `always` are
   `CURLGSSAPI_DELEGATION_POLICY_FLAG` and `CURLGSSAPI_DELEGATION_FLAG`.
5. The no-function options leave `KerberosLevel` and the six TLS-SRP members at libcurl's default,
   and `--ntlm-wb` adds no `CURLAUTH_NTLM_WB` bit.
6. The HTTP message signature settings are four Curl-specific members of `EasyAuthenticationOptions`
   (`HttpSignatureAlgorithm`, `HttpSignatureKey`, `HttpSignatureKeyId`, `HttpSignatureHeaders`), each
   documented as having no `CURLOPT_*`; the key is a secret, redacted by `ToString`.

## Consequences

- The engine can read the authentication group from one record once it references
  `Curl.Api.UnitLibrary` (BL-2323); the retarget of its classes is a task of its own.
- A command line's authentication record always describes what the engine does, even where
  `--libcurl`'s source names more bits.
