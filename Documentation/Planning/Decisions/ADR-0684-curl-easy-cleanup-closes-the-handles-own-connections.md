# ADR-0684 — curl_easy_cleanup closes the connections the handle opened, and an FTP -I transfer remembers its directory

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2629.

## Context

Upstream case 661 (lib661) runs eight FTP transfers over fresh, reused and reset easy handles. curl 8.22.0's
`curl_easy_perform` runs each handle through its own `multi_easy`, whose connection pool is the handle's alone, so
`curl_easy_cleanup` closes the handle's connections and an FTP control connection sends `QUIT`; the next handle logs
in again. The managed engine pools every handle's connections in one engine-wide `ConnectionCache` (ADR-0542), so a
fresh handle reused the previous handle's FTP connection, with no `QUIT` and no login.

Its last two transfers then showed a second gap: after a `CURLOPT_NOBODY` transfer of the home directory the reused
connection sent `CWD /` before the next one, as Curl's FTP session remembered no directory after a `-I` transfer,
while curl's `ftp_done` remembers the path's directory part (`prevpath`) after every transfer whose `CWD`s did not fail.

## Decision

1. `ConnectionCache.OpeningOwner` is an `AsyncLocal` owner each connection a `PoolingConnector` opens over the cache
   in that flow records (`PoolEntry.Owner`); `ConnectionCache.CloseIdleOwnedByAsync(owner)` closes the owner's idle
   connections, never one in use.
2. A `ManagedEasyTransfer` perform from the options sets itself as the engine cache's opening owner, unless it is in
   a multi handle (whose connections are the multi's, as libcurl's) or the curl tool's (`ToolSettings` set; its run's
   connections outlive each transfer's handle). `DisposeAsync` closes the handle's idle connections in the engine's
   cache, so an FTP one sends `QUIT`. A share handle sharing connections keeps them in its own cache, untouched.
3. Connections stay engine-wide while handles live: a second live handle may still reuse a first one's idle
   connection (`PerformAsync_TwoHandlesOfOneEngine_ShareTheCookiesAndThePooledConnection`), which libcurl would not do
   without a share. Only cleanup changed, as that is all the measured cases need; matching libcurl there too would
   make every handle own a cache, which changes `%{conn_id}` numbering across handles.
4. `FtpSession.ReportHeadAsync` remembers the path's directory part as the kept connection's previous path, without a
   `-v` line, so a later transfer on the reused connection to the same directory sends no `CWD`. curl's `Remembering
   we are in dir` line for a `-I` transfer is not written: it was not measured, and the curl tool's `-I` output is
   unchanged.

## Consequences

Upstream case 661 passes and is on the ratchet. A Libcurl or Curl.Api handle cleaned up now closes its connections,
as libcurl's, so an application creating a handle per request opens a connection per request, as with libcurl.
