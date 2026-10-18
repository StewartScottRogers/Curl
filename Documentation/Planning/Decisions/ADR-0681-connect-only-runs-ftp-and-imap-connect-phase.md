# ADR-0681 — A CONNECT_ONLY 1 perform of an FTP or IMAP URL runs the protocol's connect phase

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2623.

## Context

`ManagedEasyTransfer` answered `CURLOPT_CONNECT_ONLY` 1 with a raw TCP (or TLS) connect for every scheme
(`PerformConnectOnlyAsync`, ADR-0608). libcurl runs the protocol's connect phase before it stops: FTP logs in
(`USER`, `PASS`, `PWD`) and IMAP sends `CAPABILITY` (with `STARTTLS` and a login when asked). Upstream cases 597
(FTP) and 660 (IMAP), both lib597 through a multi handle, failed because nothing reached the server. The multi path
was not at fault: it runs each transfer's own perform. BL-2705 and BL-2706 gave the FTP and IMAP handlers
connect-only entry points (`FtpProtocolHandler.ConnectOnlyAsync`, `ImapProtocolHandler.ConnectOnlyAsync`) that hand
the open connection back.

## Decision

1. Under `CONNECT_ONLY` 1, an `ftp`, `ftps`, `imap` or `imaps` URL runs the normal perform path over a dispatcher of
   `FtpConnectOnlyHandler` and `ImapConnectOnlyHandler` (`ManagedCurlEngine.CreateConnectOnlyLoginHandlers`), as
   `CONNECT_ONLY` 2 runs over `WebSocketUpgradeOnlyHandler` (ADR-0616). Their protocol handlers dial through
   `CreateConnectOnlyConnector`'s connector, so the connection is the handle's own and never pooled.
2. On success the open (for IMAP, the `STARTTLS`-secured) connection goes to the handle's `ConnectOnlyConnection`,
   which `curl_easy_recv` and `curl_easy_send` use. A failure answers the handler's code and keeps nothing.
3. Every other scheme keeps the raw connect. POP3, SMTP and the rest have no connect-only entry point yet; a later
   task adds one when an upstream case needs it, and adds its scheme to `LogsInBeforeConnectOnly`.

## Consequences

Upstream cases 597 and 660 pass and are on the ratchet. `ManagedEasyTransferTests.ConnectOnly.cs` pins the FTP login
and the IMAP `CAPABILITY` sent before the perform ends, and a refused FTP login answering `CurlCode.LoginDenied`.
