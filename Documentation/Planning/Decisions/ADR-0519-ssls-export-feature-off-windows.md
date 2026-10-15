# ADR-0519: `-V` lists `SSLS-EXPORT` off Windows

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2331 (gap finding GF-0088)
- Amends: ADR-0021 (its `SSLS-EXPORT` row)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0021's feature table leaves `SSLS-EXPORT` out, saying no measured build prints it. The gap
analysis measured curl 8.22.0 built from the release tarball with OpenSSL on Linux
(`gap-platforms.yml`, ADR-0505): its `Features:` line lists `SSLS-EXPORT` after `SSL`. Upstream's
`docs/cmdline-opts/version.md` defines it as TLS session export and import, as `--ssl-sessions`
does. Curl serves `--ssl-sessions` off Windows: the run loads the file's tickets into its TLS
session cache and saves them after the transfers (ADR-0319, `TlsSessionFileLines` in
`Curl.Console`). The Schannel reference refuses `--ssl-sessions` and does not list the feature,
and Curl's Windows build refuses it too (ADR-0397).

## Decision

- `CurlVersionText.FeaturesLine`, the Linux and macOS line, lists `SSLS-EXPORT` between `SSL` and
  `threadsafe`, curl's case-insensitive order.
- `CurlVersionText.WindowsFeaturesLine` stays without it.
- ADR-0021's `SSLS-EXPORT` row ("No") holds only for Windows from now on.

## Consequences

`curl -V` on Linux and macOS lists `SSLS-EXPORT` as the OpenSSL reference does, so the gap item
`features:SSLS-EXPORT` measures `match` there. `CurlVersionTextTests` pins both lines.
