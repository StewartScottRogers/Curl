# ADR-1105: az:// and abfs:// URLs download, upload in blocks and list, with a SAS or an Entra ID token

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4126
- Decided by Claude under Stewart's delegation.

## Context

The Azure Storage extension (ADR-0999) had its HTTP/1.1 client, its XML and JSON bodies, its URL
mapping and its SAS and bearer authorization (BL-4288, BL-4289, BL-4290), but no handler. It follows
the Azure Blob Storage REST API and the Azure Data Lake Storage Gen2 REST API at service version
`2025-01-05` (sent as `x-ms-version`), and is tested against them, not against curl.

## Decision

- Schemes `az`, `abfs` and `abfss`, served by one handler, `AzureStorageProtocolHandler`, registered in
  `ExtensionCatalogue.cs`; its description, named `azure-storage`, is in `curl --extensions` and
  `curl --ai-help extensions`. No new option: `--oauth2-bearer` or `AZURE_STORAGE_BEARER_TOKEN` sends an
  Entra ID token as `Authorization: Bearer`, else `AZURE_STORAGE_SAS_TOKEN` is appended to the query,
  else the request is anonymous.
- Every request goes over HTTPS to port 443 of the mapped host, one request per connection, as the S3
  extension does (ADR-1090). `abfs` is TLS as well as `abfss`: the services refuse plain HTTP by default.
- `az://account/container/blob`: Get Blob downloads; `-T` sends Put Blob (`x-ms-blob-type: BlockBlob`)
  below 8 MiB, else Put Block per 8 MiB block (IDs the Base64 of a six-digit index, so all have one length)
  then Put Block List of `Latest` entries; an empty blob name or one ending in `/` sends List Blobs with
  `delimiter=/` and `prefix`, following `NextMarker`, and writes the `BlobPrefix` names then the blob names,
  one per line. A URL with no container fails with exit 3.
- `abfs[s]://filesystem@account/path`: Path Read downloads; `-T` sends Path Create (`resource=file`), a
  Path Update `action=append` per 8 MiB block at its `position`, then `action=flush` at the length; an
  empty path or one ending in `/` sends Path List (`resource=filesystem&recursive=false&directory=`),
  following `x-ms-continuation`, and writes each name, a directory with a trailing `/`.
- A response of 300 or above fails with exit 22, `Azure Storage returned error <status>: <Code>: <Message>`
  from the `Error` body, or only the status when the body is not one; an unreadable listing body or HTTP
  framing fails with exit 8; a URL the mapping rejects fails with exit 3; a malformed SAS or bearer token
  with exit 67; a refused connection with the connector's code.

## Left out, and why

- Shared Key signing (BL-4290's Notes): it needs the account key on the command line; SAS and Entra ID
  cover the same access without it.
- The DFS JSON error body (`{"error":{...}}`) is not parsed: such a failure names only its status.
- Recursive listing, ranges and `-I` metadata reads: not part of the task; each can be a later task.
