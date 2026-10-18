# ADR-0672 — `CURLOPT_PORT` is written into the URL a perform transfers

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2606.

## Context

`Libcurl` stored `CURLOPT_PORT` in `EasyNetworkOptions.Port`, but the engine never read it, so upstream cases
521 (`ftp://127.0.0.1/521/` with the FTP port), 522 (`http://127.0.0.1/522` with port 8990) and 523
(`http://www.example.com:999/523` through a proxy with port 19999) connected to the URL's port and sent it in
the request line and `Host` header. curl 8.22.0's `parse_remote_port` in `lib/url.c` applies the option by
setting `CURLUPART_PORT` on the transfer's URL handle, so everything later read from the URL - the connect,
the request target, a proxy's absolute target, the `Host` header and `CURLINFO_EFFECTIVE_URL` - carries it.

## Decision

- The engine applies it, not `Libcurl`: `ManagedEasyTransfer`'s perform from the options runs with
  `UrlPortOverride.Apply`, which, for a non-zero `Port` and a URL that parses, sets the port on a
  `ManagedCurlUrlHandle` holding the URL (`CURLU_GUESS_SCHEME`, `CURLU_NON_SUPPORT_SCHEME`, and
  `CURLU_PATH_AS_IS` under `CURLOPT_PATH_AS_IS`) and transfers the URL it gives back, as libcurl does.
  The multi handle's transfers perform the same way, so they are covered too; doing it in `Libcurl` would have
  had to rewrite the stored `CURLOPT_URL`, which a later `CURLOPT_PORT` 0 could not undo.
- A URL that does not parse is left as it is, so the perform still answers its own malformed-URL code and text.
- Applied on every perform from the options: the curl tool has no option that sets `Port`, so its transfers
  do not change.

## Consequences

Cases 521, 522 and 523 pass and hold the ratchet. libcurl stops applying the port after a redirect to another
host (`state.allow_port`); Curl's redirect follower takes each hop's URL from the `Location` header, so a hop
to another host already uses that URL's own port. The tests are `UrlPortOverrideTests`,
`ManagedEasyTransferTests.Port.cs` and `CurlEasyTests.Perform_PortOptionOnTheManagedEngine_TransfersTheUrlWithThatPort`.
