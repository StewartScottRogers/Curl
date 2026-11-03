# ADR-1213: An Azure Storage block upload stays within 50,000 blocks

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4611)

## Context

Put Block List commits a blob from at most 50,000 blocks (Blob service REST API 2025-01-05); a block may be up
to 4000 MiB. ADR-1105's `az://` upload stages fixed 8 MiB blocks, so an upload over 390 GiB would stage more
than 50,000 and fail only at Put Block List, after every byte was sent. Upstream curl has no `az://`, so there
is no curl behaviour to match.

## Decision

- When the upload's length is known (a seekable stream, as `-T file` gives), the block size is 8 MiB or, when
  that needs more than 50,000 blocks, the smallest size that needs no more:
  `AzureBlobOperations.BlockSizeFor`. Growing the block keeps every upload the service can take working,
  which is what a user of `-T` wants, rather than failing a large file.
- A block is one byte array, so the largest is `Array.MaxLength` (just under 2 GiB, below the service's
  4000 MiB). An upload that needs more than 50,000 of those (about 100 TiB) fails with exit 63
  (`CURLE_FILESIZE_EXCEEDED`, "maximum file size exceeded") before the first Put Block, message
  `An Azure Storage upload of <n> bytes needs more than 50000 blocks`.
- When the length is not known (stdin, a pipe), blocks stay at 8 MiB and the transfer ends with exit 63
  before the Put Block that would make a 50,001st block, message
  `An Azure Storage upload needs more than 50000 blocks of <size> bytes`; the staged blocks are never
  committed, so no partial blob is created. Exit 63 is the code curl gives a transfer over a size limit.

## Consequences

No `az://` upload asks Put Block List to commit more than 50,000 blocks. `AzureBlobOperations` and
`AzureStorageProtocolHandler` take the block limit as a parameter so tests pin both paths with a small one;
`AzureBlobOperationsTests` and `AzureStorageProtocolHandlerTests` pin the rule. The `abfs://` append path has
no block count and is unchanged.
