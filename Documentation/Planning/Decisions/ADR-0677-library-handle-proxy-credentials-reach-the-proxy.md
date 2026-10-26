# ADR-0677 — A library handle's `CURLOPT_PROXYUSERPWD` reaches the proxy

- **Status:** Accepted; amended by [ADR-0825](ADR-0825-the-proxy-urls-user-information-wins-over-proxy-user.md) (the proxy URL's user information wins over these options)
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2660.

## Context

Upstream case 1588 (`lib1588`) sets `CURLOPT_PROXYUSERPWD` `silly:person` and `CURLOPT_PROXYAUTH` `CURLAUTH_DIGEST`,
gets a proxy's 407 with a Digest challenge, and must answer it with a Digest `Proxy-Authorization`, then do the
same again on the same handle after `CURLOPT_PROXY` changes. The engine's HTTP handler already answers a forward
proxy's 407 with the credential on the chosen `ProxyEndpoint` (the curl tool's `--proxy-digest -U` cases pass), but
a `Libcurl` or `Curl.Api` handle never put one there: `ManagedEasyTransfer.TransferSettingsOf` built its
`EngineTransferSettings` with `ProxyCredentials: null`, so `CURLOPT_PROXYUSERPWD`, `CURLOPT_PROXYUSERNAME` and
`CURLOPT_PROXYPASSWORD` were stored and never read. The retry after the 407 went without the header.

## Decision

- `ManagedEasyTransfer.ProxyCredentialsOf` turns the three options into the `-U` credential the proxy selector
  takes (`TransferProxySelection.TrySelect`'s `proxyUser`), which replaces the proxy URL's own user information,
  as libcurl's `CURLOPT_PROXYUSERPWD` does. `CURLOPT_PROXYUSERPWD` is split at its first colon (no colon is an
  empty password, as the tool's `-U`); `CURLOPT_PROXYUSERNAME` and `CURLOPT_PROXYPASSWORD`, when set, each win over
  their half of it. None of the three set is no credential, leaving the proxy URL's.
- libcurl lets whichever of these options was set last win per half; `EasyAuthenticationOptions` keeps the three
  apart and not their order, so the separate options always win. That is the simplest rule that matches every
  upstream case, which set either the pair or the joined option, never both.

## Consequences

- Upstream case 1588 passes and is on the ratchet. The curl tool is unchanged: its runner sets `ToolSettings`,
  whose `ProxyCredentials` is `-U`.
- A handle that sets both `CURLOPT_PROXYUSERNAME` and, later, `CURLOPT_PROXYUSERPWD` sends the separate user name,
  where libcurl would send the later one's.
