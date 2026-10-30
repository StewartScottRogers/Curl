# ADR-1121 — The WireGuard handshake takes an injected clock and random source and is tested against a responder written from the whitepaper

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4323.

## Context

BL-4323 asks `Curl.Extension.WireGuard.UnitLibrary` to run the initiator's Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s
handshake and the transport data layer of the WireGuard whitepaper (sections 5.4.2 to 5.4.7 and 6), with no network
in its tests. Several choices the whitepaper leaves to an implementation had to be made.

## Decision

- **Bytes in, bytes out.** `WireGuardHandshakeInitiator` writes initiations and consumes responses and cookie replies
  as byte arrays; `ConnectAsync` drives one exchange over an `IWireGuardDatagramChannel` it is handed. Retransmission
  after Rekey-Timeout belongs to the caller (BL-4147's tunnel), which cancels and calls again.
- **Injected clock and randomness.** A `TimeProvider` gives TAI64N timestamps, the cookie's 120-second life and the
  session timers; a `System.Security.Cryptography.RandomNumberGenerator` instance gives ephemeral keys and indices,
  so tests substitute a repeatable one without a package.
- **TAI64N as the Linux module and wireguard-go write it:** seconds offset by 2^62 + 10, nanoseconds rounded down to
  a multiple of 2^24, which also limits a peer to about 60 initiations a second.
- **A cookie reply sends the next initiation at once** with mac2, rather than waiting for the retransmission timer;
  the responder accepts it either way, and the caller has no timer yet.
- **RFC 6479 window of 128 64-bit blocks** (8128 counters), the size wireguard-go uses for 64-bit hosts. A counter is
  checked before opening and marked only after the tag verifies.
- **Section 6.1's timers on the session:** `NeedsRekey` after 120 s or 2^60 messages; `Seal` throws and `TryOpen`
  refuses after 180 s or 2^64 - 2^13 - 1 messages. Padding is to a multiple of 16 without the MTU cap, which belongs
  to the IP layer that knows the MTU.
- **Tested against a responder written in the tests** straight from the whitepaper on `Curl.Cryptography` alone, not
  on the library's `NoiseSymmetricState`, so the initiator is checked against a second reading of the specification.

## Consequences

The handshake and transport layers are deterministic under test and need no network. BL-4147 adds the timers that
resend an initiation and start a new handshake when `NeedsRekey` is true.
