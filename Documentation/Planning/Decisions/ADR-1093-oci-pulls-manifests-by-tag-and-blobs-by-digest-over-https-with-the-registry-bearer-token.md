# ADR-1093: OCI pulls manifests by tag and blobs by digest over HTTPS with the registry Bearer token

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4292)

## Context

BL-4292 adds pulling from an OCI registry as an extension (ADR-0999), tested against the OCI Distribution
Specification 1.1.0 (https://github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md), "Pulling manifests",
"Pulling blobs" and "Error Codes", and the Docker/Distribution token authentication specification, not against curl,
which has no such scheme. It needs a URL form, a transport, an authentication flow and exit codes for the failures.

## Decision

- Scheme `oci`, served by `Curl.Extended.Protocol.OciRegistry.UnitLibrary`'s `OciRegistryProtocolHandler`; it adds no
  command-line option. The URL is `oci://registry[:port]/name[:tag]` for a manifest, the tag `latest` when none is
  given (as `docker pull` does), and `oci://registry[:port]/name@digest` for a blob. Name, tag and digest follow the
  specification's grammar; a path that breaks it fails with exit 3 before any connection.
- Always HTTPS, port 443 when the URL names none, through curl's connector, so `-k`, `--cacert` and the other TLS
  options apply. A manifest pull sends `Accept` with the OCI image manifest and index and the Docker manifest and
  manifest list media types, so the registry returns what it holds; a blob pull sends curl's `*/*`.
- A 401 with a `Bearer` challenge is answered once: `GET` the realm with `service` and `scope` (with `-u`'s
  credentials as HTTP Basic when given, otherwise anonymous), then the request again with `Authorization: Bearer`.
- 301, 302, 303, 307 and 308 are followed, as a registry may send a blob to storage, at most 50 times (curl's
  `--max-redirs` default, exit 47 beyond it), without the token, which the storage did not issue.
- A 2xx body is written to the output whole. Any other final status fails: `UNAUTHORIZED`, `DENIED`, a 401 or a 403
  with exit 67 (login denied), everything else with exit 22, both with curl's `-f` message
  `The requested URL returned error: N` followed by the first `errors[].code` and `message` in parentheses. A token
  endpoint that gives no token fails with exit 67.
- With `CURL_EXTENSIONS=0` the scheme is not registered and fails as curl fails any unsupported scheme (exit 1).

## Left out, and why

- Plain-HTTP registries: there is no curl option that says "this registry has no TLS" without an extension option of
  its own; a later task can add one.
- Docker Hub's short names (`alpine` for `registry-1.docker.io/library/alpine`): a client convention, not the
  specification; the full name works.
- `name:tag@digest` and manifests pulled by digest: the URL form keeps `@` for blobs, so each URL names one thing.
- Digest verification of the pulled content: curl writes what the server sends; `sha256sum` checks it.
- Pushing, listing tags, the referrers API and HTTP Basic-only registries: pull of manifests and blobs is this task's
  scope.
- Proxies: the registry HTTP client of BL-4291 connects without curl's proxy settings.

## Consequences

`curl oci://ghcr.io/owner/image:tag` prints the manifest and `curl -o layer oci://ghcr.io/owner/image@sha256:...`
saves a layer. The description appears in `curl --extensions` and `curl --ai-help extensions`.
