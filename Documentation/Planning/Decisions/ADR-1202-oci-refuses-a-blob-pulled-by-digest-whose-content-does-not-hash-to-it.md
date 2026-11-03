# ADR-1202: OCI refuses a blob pulled by digest whose content does not hash to it, with exit 8

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4591)

## Context

The OCI Image Specification (descriptor, "Digests") says content fetched by digest SHOULD be verified against the
digest before it is consumed. `oci://registry/name@digest` wrote whatever body the registry, or the storage a redirect
led to, answered with. `oci://` is an extension (ADR-0999), so there is no curl behaviour to match; the exit code is
chosen from curl's own codes.

## Decision

`OciRegistryProtocolHandler` passes the body of a blob pull, after any redirects, to `OciPullFlow.VerifyDigest`, which
hashes it with the digest's algorithm and compares the lowercase hexadecimal hash with the digest's encoded part. A
mismatch fails the transfer with exit 8 (`CURLE_WEIRD_SERVER_REPLY`, the server answered with something the protocol
does not allow), the message `The pulled content does not match its digest <digest>.`, and nothing written to the
output, since the body is checked before it is written. Exit 25, which a push with a mismatching digest uses, names an
upload and does not fit a pull. The registered algorithms `sha256` and `sha512` are checked; a digest of any other
algorithm that meets the grammar passes unchecked, as the specification allows for an unrecognised algorithm. A
manifest pulled by tag has no digest and is not checked.

## Consequences

Tampered or corrupted blobs no longer reach scripts. The conformance case `OciRegistry:IMAGE-Descriptor-Digests-1`
passes. Blobs are already held whole in memory, so hashing them costs no extra buffering.
