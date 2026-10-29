# ADR-0992: WinLDAP's own ldaps:// connection honours -k

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4044, for CGR-289). Amends ADR-0979.

## Context

ADR-0979 gave WinLDAP's second `ldaps://` connection a handshake that trusts only the Windows
certificate store, and `ISystemStoreTrustTlsProvider.SystemStoreOnly` also turned `-k` off. Measured on
2026-10-28 with curl 8.22.0 (Schannel, WinLDAP) and `Record-CurlExchange.ps1 -Tls -Script` serving a
throwaway self-signed certificate: `curl -sS -k ldaps://localhost:PORT/dc=test` completes the second
handshake and sends its three rootDSE searches and an NTLM bind there. Curl failed that handshake with
exit 60 (`SEC_E_UNTRUSTED_ROOT`).

## Decision

`SystemStoreOnly` drops `--cacert`, `--capath` and a CA blob but keeps `Insecure`: with `-k` the second
connection does not verify the server, as curl's WinLDAP connection does not; without it the server
certificate must verify against the system store.

## Consequences

`-k` over `ldaps://` now reaches the bind through Curl, as through curl. The NTLM flags of that bind
and the error text once the server stops answering still differ; BL-4044's follow-up covers them.
