# ADR-1286: --xattr on Windows writes a Zone.Identifier stream, as curl 8.22 does

- Status: Accepted
- Date: 2026-11-16
- Decided by Claude under Stewart's delegation (BL-5338)

## Context

ADR-0320 gave `--xattr` a libc-only writer and no writer on Windows, because no Windows curl then wrote extended
attributes. curl 8.22 writes the `Zone.Identifier` alternate data stream there (`[ZoneTransfer]`, `HostUrl=`, and
`ReferrerUrl=` when a `Referer` was sent). The outside review STD-088 measured `HostUrl=http://127.0.0.1:30080/hello?q=1#f`
for `http://u:p@127.0.0.1:30080/hello?q=1#f`, credentials removed, the original URL under `-L`.

## Decision

- On Windows `NativeExtendedAttributeWriter.ForCurrentPlatform()` gives a `WindowsZoneIdentifierWriter`, which keeps the
  `IExtendedAttributeWriter` seam: the referrer attribute is held until the origin URL attribute arrives, then one stream
  `<file>:Zone.Identifier` is written: `[ZoneTransfer]\r\nHostUrl=<url>\r\n` plus `ReferrerUrl=<referer>\r\n` when set.
  `user.creator` and `user.mime_type` have no place in the stream and are accepted without a write.
- The content logic is a covered type; only the stream write is a thin delegate in the excluded native adapter (ADR-0083).
- Linux, macOS and FreeBSD keep the libc writer (ADR-0320). This reverses ADR-0320's "Windows writes none".

## Consequences

The manual text for `--xattr` (curl's own) already describes `HostUrl` and `ReferrerUrl`; `--help` is unchanged.
