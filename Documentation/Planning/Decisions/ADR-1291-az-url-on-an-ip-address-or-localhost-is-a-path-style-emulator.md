# ADR-1291: An az:// URL on an IP address or localhost is a path-style emulator

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5407).

## Context
Azurite serves the Blob service path-style: `/devstoreaccount1/ctr?restype=container&comp=list`. Curl took the host as the account and the first path segment as the container, so an emulator container could not be listed.

## Decision
When the `az://` host is an IPv4 or IPv6 address or `localhost`, the host is used as it is (no `.blob.core.windows.net` suffix), the first path segment is the account and the second the container. `AzureStorageEndpoint.AccountInPath` carries this. List Blobs goes to `/account/container`; blob GET and PUT keep the account in the path. Every other host maps as before.

## Consequences
`az://127.0.0.1:10000/c/x` now means account `c`, container `x`. Documented in `AzureStorage.md` and `--extended-help`.
