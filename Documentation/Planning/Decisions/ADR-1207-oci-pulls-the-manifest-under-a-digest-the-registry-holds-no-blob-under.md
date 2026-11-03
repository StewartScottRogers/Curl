# ADR-1207: OCI pulls the manifest under a digest the registry holds no blob under

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4592)

## Context

ADR-1093 gave `oci://registry/name@digest` one meaning: the blob under that digest, `GET /v2/<name>/blobs/<digest>`.
The OCI Distribution Specification 1.1 ("Pulling manifests") also pulls a manifest by digest,
`GET /v2/<name>/manifests/<digest>`, and `docker pull name@digest` uses the same `@digest` form for it. With one URL
form for both, a manifest stored only under its digest was unreachable: the blob request failed with 404
(`BLOB_UNKNOWN`) and exit 22. `oci://` is an extension (ADR-0999), so there is no curl behaviour to match.

## Decision

An `@digest` pull asks for the blob first, as before, and when the registry answers it 404 (after any token exchange
and redirects), asks `GET /v2/<name>/manifests/<digest>` with the manifest media types in `Accept`
(`OciPullFlow.PullWithFallbackAsync`, `OciPullReference.ManifestApiPath`). Whatever is pulled is checked against the
digest (ADR-1206), since a manifest's digest is the hash of its bytes too. When the manifest is not found either, the
failure is the blob's 404, so a missing blob reports what it did before; any other failure of the manifest request is
reported as its own. A blob request that fails with anything but 404 does not fall back.

A distinct syntax for manifest digests was rejected: `@digest` is the form every OCI tool uses for both, and a registry
never stores a blob and a manifest of different content under one digest, so trying both cannot pull the wrong thing.

## Consequences

The conformance case `OciRegistry:DIST-PullManifests-2` passes. A pull of a missing digest costs a second request.
