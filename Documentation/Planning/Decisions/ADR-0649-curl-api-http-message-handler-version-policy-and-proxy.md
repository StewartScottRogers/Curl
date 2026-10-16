# ADR-0649 — `CurlHttpMessageHandler` holds the HTTP version policy as `SocketsHttpHandler` does and takes `HttpClientHandler`'s proxy and credential properties

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2511.

## Context

ADR-0647 maps `HttpRequestMessage.Version` and `VersionPolicy` onto `CURLOPT_HTTP_VERSION`, but nothing
checked that the engine's answer met the policy, a version the engine cannot speak went through, and
the handler had no way to set a proxy or credentials the way `HttpClient` users do.

## Decision

- **Versions the engine speaks:** 1.0, 1.1, 2.0 and 3.0. A higher version under
  `RequestVersionOrLower` is asked for as 3.0; any other version (0.9, 1.2, 4.0 under `Exact` or
  `OrHigher`) throws `HttpRequestException` with `HttpRequestError.VersionNegotiationError` before a
  handle is created, as `SocketsHttpHandler` refuses a version it does not speak.
- **The response's version is checked against the policy** by major version: `Exact` needs the same,
  `OrHigher` no lower; `OrLower` accepts any, since the engine never goes above the version asked for
  (an existing test's HTTP/2 answer to the default 1.1 request stays accepted). A refused response's
  body is disposed, aborting the transfer, and `HttpRequestException` (`VersionNegotiationError`) is
  thrown. A response with no HTTP version (`HttpVersion.Unknown`) passes every policy. The check
  happens after the transfer because libcurl falls back silently (`CURL_HTTP_VERSION_2_0` to 1.1, `3`
  to 2 or 1.1); asking for an `-only` variant under `OrHigher` would refuse a higher version too.
- **Proxy and credentials** take `HttpClientHandler`'s names, so code moving from it compiles:
  `Proxy` (`IWebProxy`), `UseProxy` (default true), `DefaultProxyCredentials` and `Credentials`, read
  per request. A proxy's `GetProxy(uri)` becomes `CURLOPT_PROXY` as scheme, host and port on the
  client's `CurlProxyOptions` (its other settings kept); a bypassed host, a proxy that gives none, and
  `UseProxy = false` set `CURLOPT_PROXY` to `""`, which reaches the host directly past the proxy
  environment variables. Bypass is asked per request through `IsBypassed`, not by translating
  `WebProxy.BypassList` regular expressions into `CURLOPT_NOPROXY` host names, which cannot express them.
- **Credentials** become `CURLOPT_USERNAME`/`CURLOPT_PASSWORD` and `CURLOPT_PROXYUSERNAME`/
  `CURLOPT_PROXYPASSWORD` on the client's `CurlAuthenticationOptions`, from `GetCredential(uri, "Basic")`,
  the proxy's own credentials winning over `DefaultProxyCredentials`; a domain is sent as
  `domain\user`, as libcurl takes it for NTLM. The authentication methods stay the client's: the handler
  does not guess them.

## Consequences

- Code that relied on a 2.0 `OrHigher` or `Exact` request silently getting HTTP/1.1 now gets the
  exception `SocketsHttpHandler` would give.
- With `Credentials` set and the client's default `Basic` method, the credentials are sent on the
  first request, as curl's `-u` sends them, rather than only after a challenge as `HttpClientHandler` does.
