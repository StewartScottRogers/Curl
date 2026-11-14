# ADR-1266: Any certificate the --cacert file lists anchors the Schannel chain

- Status: Accepted
- Date: 2026-11-13
- Task: BL-5100 (outside fitness review finding STD-055)
- Decided by Claude under Stewart's delegation.

## Context

curl's Schannel backend builds the `--cacert` chain with `CERT_CHAIN_EXCLUSIVE_ENABLE_CA_FLAG`, so any
certificate the file holds, an intermediate CA or the leaf, is a trust anchor. ADR-0797 took only the leaf
as a candidate in the Schannel build, so a bundle holding just an issuing intermediate gave exit 60.
.NET's `CustomRootTrust` reports `PartialChain` for such an anchor.

## Decision

`ServerCertificateVerification.CaCertificateFileAnchorsChain` takes every chain element as an anchor
candidate in both builds (the OpenSSL build already did, via partial chain). The chain's only errors must
still be `PartialChain` or `UntrustedRoot`, so an expired certificate or an unrelated anchor stays exit 60,
and the name check runs afterwards unchanged. The proxy policy shares this code, so `--proxy-cacert` follows.

## Consequences

The old test pinning exit 60 for an intermediate-only file now pins acceptance. Not measured against real
curl here (no Windows hand check in a lane); the task text records curl 8.22.0's measured behaviour.
