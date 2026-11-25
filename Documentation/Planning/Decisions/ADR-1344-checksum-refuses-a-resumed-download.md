# ADR-1344: --checksum refuses a resumed download (BL-5837)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context
`--checksum` hashes the bytes the transfer writes. With `-C -` the transfer writes only the bytes after the resume offset, so the digest of the whole file never matched and a correct resume exited 61 (EXT-109). The hook has no path to the existing file prefix, and the output stream is write-only.

## Decision
The hook refuses a transfer whose `ResumeFrom` is above 0 with exit 2 and a message saying so, before the transfer runs (one of the two alternatives the finding names). Offset 0 hashes as before. Seeding the hash from the file prefix needs the output path in `ITransferContext`; a later task may add it.

## Consequences
`--checksum` with `-C -` on a partial file exits 2 instead of a misleading 61; a wrong digest on a full download still exits 61.
