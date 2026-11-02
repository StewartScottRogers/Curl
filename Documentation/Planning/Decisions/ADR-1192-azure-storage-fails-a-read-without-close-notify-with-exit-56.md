# ADR-1192: Azure Storage fails a read without close_notify with exit 56

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4540
- Decided by Claude under Stewart's delegation.

## Context

BL-4520 found that an `az://` download whose response is close-delimited (`Connection: close`, no
length) over a TLS connection that ends without the server's `close_notify` let
`MissingCloseNotifyException` escape `AzureStorageProtocolHandler.ExecuteAsync`: the extension's HTTP
reader (`AzureStorageHttpConnectionReader`) did not expect the `IOException` the secure connection
throws (ADR-0221), and the handler only turned `AzureStorageHttpException` into a result.

## Decision

The extension treats a missing `close_notify` the way curl's HTTP layer does. ADR-0221 measured curl
8.21.0 (mingw, Schannel) and curl 8.18.0 (Ubuntu, OpenSSL 3.5.5) against `Record-CurlExchange.ps1 -Tls`:
the read fails with exit 56 (`CURLE_RECV_ERROR`) and the TLS build's text, whatever the body's framing,
close-delimited included, and the bytes already written stay written. No new measurement was needed,
since the Azure extension speaks the same HTTP/1.1 over the same `IConnection`.

- `AzureStorageHttpConnectionReader` catches `MissingCloseNotifyException` and throws
  `AzureStorageHttpException` with the new `AzureStorageHttpFailure.ReceiveFailed` and the exception's
  message, so the HTTP client still fails only with `AzureStorageHttpException` (in the head and in
  every body framing).
- `AzureStorageProtocolHandler` maps `ReceiveFailed` to exit 56 and every other HTTP failure to exit 8,
  as before.

Accepting the close-delimited body as complete was rejected: no curl build does, and a drop-in
replacement must not report success where curl reports exit 56.

## Consequences

An `az://` or `abfs://` transfer cut off without `close_notify` ends with exit 56 and the platform
build's message instead of an exception. Other `IOException`s from the connection are unchanged.
