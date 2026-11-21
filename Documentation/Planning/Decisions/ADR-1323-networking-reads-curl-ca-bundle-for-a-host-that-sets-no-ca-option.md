# ADR-1323: Curl.Networking reads CURL_CA_BUNDLE for a host that sets no CA option

- Status: Accepted
- Date: 2026-11-20
- Task: BL-5642
- Decided by Claude under Stewart's delegation.

## Context

The console takes the OpenSSL build's CA file from `CURL_CA_BUNDLE` (and `SSL_CERT_*`) in `CaCertificateEnvironment`, in `Curl.Cli`, before the engine sees its options. `Curl.Api` and `Libcurl` never run that step, so their default trust source was only the system store.

## Decision

`ServerCertificateVerification.DefaultCaBundleFile` in the OpenSSL build, with no CA file or blob and no `-k`, is a non-empty `CURL_CA_BUNDLE` first, else the built-in bundle for an emptied system store as before (ADR-1168). `SSL_CERT_FILE` and `SSL_CERT_DIR` need no step: .NET's OpenSSL store reads them itself. The Schannel build reads none. A `CURL_CA_BUNDLE` naming an unusable file is exit 77, as in curl. The console's own result is unchanged, since it sets the same file on the options.

## Consequences

All three hosts share one default trust source. The 77 seen from `Curl.Api` on Linux and macOS could not be reproduced on Windows; if it persists after this, its cause is elsewhere and a new task is needed.
