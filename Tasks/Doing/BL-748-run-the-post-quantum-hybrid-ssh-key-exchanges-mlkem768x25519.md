---
id: BL-748
title: Run the post-quantum hybrid SSH key exchanges mlkem768x25519, mlkem768nistp256, mlkem1024nistp384 and sntrup761x25519
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-678, BL-743, BL-747]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-748 — Run the post-quantum hybrid SSH key exchanges mlkem768x25519, mlkem768nistp256, mlkem1024nistp384 and sntrup761x25519

## Goal

The SSH transport runs `mlkem768x25519-sha256`, `mlkem768nistp256-sha256`, `mlkem1024nistp384-sha384`, `sntrup761x25519-sha512` and `sntrup761x25519-sha512@openssh.com` when ADR-0122's full algorithm set is selected, deriving the shared secret and exchange hash exactly as libssh 0.12.2 and OpenSSH do, with ML-KEM from BL-743, sntrup761 from BL-747, X25519 from BL-671 and the NIST curves from the BCL's `ECDiffieHellman`.

## Context

- ADR-0122 lists these five in the full set's key-exchange order (libssh 0.12.2, measured 2026-09-28); neither reference build (libssh2 1.11.1) offers them, so they are not in either default set.
- Specifications: draft-ietf-sshm-mlkem-hybrid-kex (mlkem768x25519-sha256, mlkem768nistp256-sha256, mlkem1024nistp384-sha384), draft-josefsson-ntruprime-ssh (sntrup761x25519-sha512), OpenSSH `kexmlkem768x25519.c` and `kexsntrup761x25519.c`.
- Builds on BL-564's key-exchange seam and BL-678's X25519 exchange.

## Acceptance criteria

- [ ] Each of the five exchanges completes against ADR-0122's in-memory SSH peer in `Curl.Protocol.Ssh.UnitTests`, with fixed keys and an injected random source, and the derived session keys match values computed independently in the test.
- [ ] A malformed server share (wrong length) fails the key exchange with the exit code and message ADR-0122 gives for a failed key exchange, pinned by a test.
- [ ] `dotnet build` clean, fast tests green, 100% line and branch coverage of the new code.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
