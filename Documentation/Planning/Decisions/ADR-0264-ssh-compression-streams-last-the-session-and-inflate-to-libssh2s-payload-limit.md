# ADR-0264 — SSH compression streams last the session and inflate to libssh2's payload limit

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-575.

## Context

ADR-0122 decided that `--compressed-ssh` offers `zlib,zlib@openssh.com,none` in both
directions on every preset, that `zlib` starts at the first `NEWKEYS` and
`zlib@openssh.com` after `SSH_MSG_USERAUTH_SUCCESS`, and that the primitive is the BCL's
`ZLibStream`. BL-575 re-measured the offer on 2026-09-29 (Windows reference curl 8.21.0,
libssh2 1.11.1, `Record-CurlExchange.ps1 -Response 'SSH-2.0-OpenSSH_9.9\r\n'`, `scp://`):
without the option both lists are `none`; with it, both are `zlib,zlib@openssh.com,none`.

Three questions ADR-0122 left open:

1. Does the BCL's flush produce what OpenSSH accepts, and does its inflater read what
   OpenSSH sends? OpenSSH deflates each packet with `Z_PARTIAL_FLUSH`, which can end a
   packet mid-byte, and inflates with `Z_SYNC_FLUSH`.
2. What happens to a direction's stream at a key re-exchange?
3. How large may one packet inflate?

## Decision

- **The BCL is enough; no hand-built deflate.** `ZLibStream.Flush()` is a sync flush
  (the packet ends with the empty stored block `00 00 FF FF`), which zlib's inflate
  accepts whatever flush mode it runs with. `ZLibStream` in decompress mode over a
  `MemoryStream` refilled with each packet's bytes returns everything those bytes complete
  and waits for the rest, so OpenSSH's partial flushes, bits held over into the next
  packet included, inflate packet by packet. A hand-built bit-level vector of two partial
  flushes (`SshZlibDecompressorTests`) pins it, and a real OpenSSH 10 `sshd` with
  `Compression yes` in WSL served a 653 KiB SFTP download and took the same file as an
  upload over `zlib@openssh.com`, byte for byte, as the reference curl did.
- **Each direction's stream starts once, from the first key exchange's choice, and lasts
  the session.** A re-exchange changes keys only. This is what OpenSSH, the server curl
  talks to, does: it starts each stream once and keeps it across re-keys. Resetting the
  stream at a re-key would desynchronise from such a server.
- **One packet may inflate to at most 40000 bytes**, libssh2 1.11.1's
  `LIBSSH2_PACKET_MAXPAYLOAD`, the same limit the packet reader puts on a whole packet.
  More, an empty inflation, or bytes that are not a continuation of the stream are an
  `InvalidDataException`, which each reader already maps to the failure it reports for a
  broken packet.
- Compression is at zlib's default level (`CompressionLevel.Optimal`), as libssh2's
  `Z_DEFAULT_COMPRESSION`.

## Consequences

- `Curl.Protocol.Ssh.UnitLibrary` has a `Compression` folder: `SshCompressionMethods`,
  `SshZlibCompressor`, `SshZlibDecompressor`. `SshPacketWriter.StartCompression` and
  `SshPacketReader.StartDecompression` switch a direction on; `SshTransport` does it at
  `NEWKEYS` for `zlib` and in `StartDelayedCompression` for `zlib@openssh.com`, which
  `SshUserAuthentication` calls when a `SSH_MSG_USERAUTH_SUCCESS` is read.
- `InMemorySshServer.Compression` makes the fake server compress as OpenSSH does, so the
  handler tests run whole compressed transfers.
