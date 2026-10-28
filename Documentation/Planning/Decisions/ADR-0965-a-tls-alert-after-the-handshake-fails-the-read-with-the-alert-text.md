# ADR-0965: A fatal TLS alert after the handshake fails the read with the build's alert text

Status: Accepted. Decided by Claude under Stewart's delegation (BL-3752, CGR-223).

## Context

A server that sends `certificate_required` or `unknown_ca` after the handshake (mTLS with `-Verify`) made Curl print
`Failure when receiving data from the peer`. curl 8.22.0 (Schannel) prints
`schannel: failed to read data from server: SEC_E_ILLEGAL_MESSAGE (0x80090326) - ...`; curl's OpenSSL build prints
`OpenSSL SSL_read: OpenSSL/3.5.x: error:0A00045C:SSL routines::tlsv13 alert certificate required, errno 0`
(`...0A000418...tlsv1 alert unknown ca` with a client certificate). All are exit 56.

## Decision

`SslStreamConnection.ReadAsync` turns an `IOException` that `TlsFailureMessages.AlertReadFailure` recognises into a
`MissingCloseNotifyException` carrying that text, the exception whose message the HTTP handler prints unchanged.
Schannel build: a Win32 status `SEC_E_ILLEGAL_MESSAGE` or `SEC_E_DECRYPT_FAILURE` (what `SslStream` gives for the
alert record) is reported as the former. OpenSSL build: `SslStream` does not name the alert, so any non-socket
failure under the `IOException` is `certificate required` without `--cert` and `unknown ca` with it, the two
measured cases. Socket errors keep their `Recv failure` text.
