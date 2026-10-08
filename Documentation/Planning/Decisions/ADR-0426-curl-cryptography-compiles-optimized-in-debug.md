# ADR-0426 — Curl.Cryptography compiles optimized in Debug too

- **Status:** Accepted
- **Date:** 2026-10-07

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1558.

## Context

ADR-0400 made Blowfish's F function a masked scan of all 1,024 S-box words, so one bcrypt
hash (about a million F functions) costs about 33 ms in a Release build on the factory
machine. The tests run the Debug build, and a Debug assembly carries a
`DebuggableAttribute` that turns JIT optimization off for every method in it, so the same
hash took about 600 ms, and with other lanes building `BcryptPbkdfTests` took 6 to 15
seconds and wrote `SLOW:` lines. `[MethodImpl(MethodImplOptions.AggressiveOptimization)]`
does not override that attribute; measured, it made no difference.

## Decision

`Curl.Cryptography.UnitLibrary.csproj` sets `<Optimize>true</Optimize>` for every
configuration. The compiler then emits optimized IL and no `DisableOptimizations` flag, so
the JIT optimizes the library in Debug as in Release: one bcrypt hash takes about 42 ms
in Debug, and `BcryptPbkdfTests` runs in about two seconds with no `SLOW:` line. The code
and its constant-time scan are unchanged. Coverage is still measured on this build and
stays at 100% line and branch.

## Consequences

- Every primitive in the library, not just Blowfish, runs at Release speed under test,
  which shortens the whole `Curl.Cryptography.UnitTests` run.
- Stepping through the library in a debugger shows optimized code (locals may be
  unavailable). The library is bytes in, bytes out and pinned by published vectors, so
  that costs little.
- bcrypt stays well slower than OpenSSH's C, which reads its S-boxes by index; that is
  ADR-0400's price for keeping the passphrase off the cache-timing channel, and is kept.

## Alternatives considered

- **An AVX-512 scan beside the `Vector<T>` one:** measured at 44 ms against 42 ms on the
  factory machine, which runs 512-bit operations as two 256-bit halves; no gain for a
  hardware branch the coverage gate must exclude.
- **Table-indexed S-boxes, as OpenSSH:** a few milliseconds, but reopens AF-0017's
  passphrase leak that ADR-0400 closed.
- **`AggressiveOptimization` on the hot methods:** ignored in a Debug assembly.
