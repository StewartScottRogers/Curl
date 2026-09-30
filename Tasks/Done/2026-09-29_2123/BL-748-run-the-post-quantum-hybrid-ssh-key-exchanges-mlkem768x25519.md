---
id: BL-748
title: Run the post-quantum hybrid SSH key exchanges mlkem768x25519, mlkem768nistp256, mlkem1024nistp384 and sntrup761x25519
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-678, BL-743, BL-747]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0265-hybrid-post-quantum-ssh-key-exchanges-hash-k-as-a-string.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-748 — Run the post-quantum hybrid SSH key exchanges mlkem768x25519, mlkem768nistp256, mlkem1024nistp384 and sntrup761x25519

## Goal

The SSH transport runs `mlkem768x25519-sha256`, `mlkem768nistp256-sha256`, `mlkem1024nistp384-sha384`, `sntrup761x25519-sha512` and `sntrup761x25519-sha512@openssh.com` when ADR-0122's full algorithm set is selected, deriving the shared secret and exchange hash exactly as libssh 0.12.2 and OpenSSH do, with ML-KEM from BL-743, sntrup761 from BL-747, X25519 from BL-671 and the NIST curves from the BCL's `ECDiffieHellman`.

## Context

- ADR-0122 lists these five in the full set's key-exchange order (libssh 0.12.2, measured 2026-09-28); neither reference build (libssh2 1.11.1) offers them, so they are not in either default set.
- Specifications: draft-ietf-sshm-mlkem-hybrid-kex (mlkem768x25519-sha256, mlkem768nistp256-sha256, mlkem1024nistp384-sha384), draft-josefsson-ntruprime-ssh (sntrup761x25519-sha512), OpenSSH `kexmlkem768x25519.c` and `kexsntrup761x25519.c`.
- Builds on BL-564's key-exchange seam and BL-678's X25519 exchange.

## Acceptance criteria

- [x] Each of the five exchanges completes against ADR-0122's in-memory SSH peer in `Curl.Protocol.Ssh.UnitTests`, with fixed keys and an injected random source, and the derived session keys match values computed independently in the test.
- [x] A malformed server share (wrong length) fails the key exchange with the exit code and message ADR-0122 gives for a failed key exchange, pinned by a test.
- [x] `dotnet build` clean, fast tests green, 100% line and branch coverage of the new code.

## Notes

- Plan: one `HybridKemSshKeyExchange` (the name ADR-0122 gives) built from two `ISshKeyShare`s - `MlKemSshKeyShare` or `Sntrup761SshKeyShare`, then `X25519SshKeyShare` or `NistCurveSshKeyShare`. The classical `curve25519-sha256` and `ecdh-sha2-*` exchanges now use the same classical shares, so the all-zero and on-curve checks live in one place.
- Decision (ADR-0265, decided by Claude under Stewart's delegation): K enters H and the key derivation as a `string` for the hybrids (OpenSSH `sshbuf_put_string`, both drafts), so `SshKeyExchangeOutcome` now carries `EncodedSharedSecret` and `SshKeyDerivation` hashes it as given; classical methods encode with `SshExchangeHashInput.EncodeMpint`.
- Real curl cannot be measured here: libssh2 1.11.1 offers none of the five. The wire format follows draft-ietf-sshm-mlkem-hybrid-kex, draft-josefsson-ntruprime-ssh and OpenSSH's kexmlkem768x25519.c and kexsntrup761x25519.c; `TestKeyExchangeServer` computes each exchange's H and six keys independently and the transport reaches them (`ExchangeKeysAsync_EachMethod_ReachesTheServersExchangeHashAndKeys`).
- Failures: wrong-length server share, unusable classical key and an altered ciphertext each end in exit 2 `Failure establishing ssh session: -8, Unable to exchange encryption keys` (`ExchangeKeysAsync_Hybrid*_FailsWithMinus8*`).
- `ExchangeKeysAsync_MethodOrHostKeyNotImplemented_ThrowsNotSupported` used sntrup761 as the unimplemented method; it is split into `Create_MethodNotImplemented_ThrowsNotSupported` and `ExchangeKeysAsync_HostKeyNotImplemented_ThrowsNotSupported`.
- Touches widened to the new ADR and the ADR index README; no task in Doing names them (BL-717 touches HTTP, networking and the console).
- Coverage: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` gives 100% line, 100% branch, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH runs mlkem768x25519-sha256, mlkem768nistp256-sha256, mlkem1024nistp384-sha384 and sntrup761x25519-sha512 (both names) with K hashed as a string (ADR-0265)
