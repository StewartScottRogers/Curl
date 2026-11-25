# ADR-1354: oci:// refuses -r and -C with exit 4 (BL-5913)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context
The OCI registry handler ignored `-r` and `-C`. A resumed pull (`-C -`) then appended the whole manifest or blob to the
partial file and exited 0, the gap fitness finding EXT-122 expects `oci://` to share. `s3://` sends a `Range`
header (BL-5873); `az://` and `abfs://` refuse both options (ADR-1348).

## Decision
An `oci://` transfer with `-r`, or with `-C` other than offset 0, fails with exit 4 and
`oci:// does not support <option>; it transfers only whole manifests and blobs` before any connection, pulls and pushes
alike (`OciByteRangeRefusal`). `-C 0`, and `-C -` with no partial file, ask for the whole transfer and are let through.

Refusing was chosen over sending a `Range` header. A blob pulled by digest is checked against that digest before it
reaches the output (ADR-1206), and part of a blob cannot be checked: honouring `-r` would write unverified content, and
honouring `-C` would mean reading the partial file back to hash it with the rest, which the handler cannot do through
its output stream. The Distribution Specification also lets a registry ignore a range, which would need an answer of its
own. Refusing matches how `-I` and `-X` are already refused (ADR-1238) and cannot corrupt a file.

## Consequences
A script that passed `-r` or `-C` to `oci://` now gets exit 4 and its partial file is left as it was.
