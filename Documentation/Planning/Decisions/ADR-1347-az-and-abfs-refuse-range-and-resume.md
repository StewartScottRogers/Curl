# ADR-1347: az:// and abfs[s]:// refuse -r and -C with exit 4 (BL-5897)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context
The Azure Storage handler ignored `-r` and `-C`. A resumed download (`-C -`) then appended the whole blob to the
partial file and exited 0 (fitness finding EXT-122). ADR-1105 leaves ranges unimplemented, and ADR-1249 says a storage
scheme never sends a request other than the one it reports. `s3://` now sends a `Range` header (BL-5873).

## Decision
An `az://`, `abfs://` or `abfss://` transfer with `-r`, or with `-C` other than offset 0, fails with exit 4 and
`<scheme>:// does not support <option>; it transfers only whole blobs` (`files` for `abfs`) before any connection, for
downloads, uploads, lists and deletes alike (`AzureStorageByteRangeRefusal`). `-C 0`, and `-C -` with no partial file,
ask for the whole transfer and are let through.

Refusing was chosen over sending a `Range` header: it is the smaller change, it matches how `-I` and `-X` are already
refused, and it cannot corrupt a file. Honouring ranges means signing the header under Shared Key and deciding what each
status means on both the Blob and the Data Lake endpoint; that can be a later task.

## Consequences
A script that passed `-r` or `-C` to these schemes now gets exit 4 and its partial file is left as it was.
