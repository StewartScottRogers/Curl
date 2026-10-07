---
id: BL-1525
title: Make X25519, X448 and CAST-128 fast enough that their full RFC iteration tests take minutes, not half an hour
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-06
completed:
---
# BL-1525 — Make X25519, X448 and CAST-128 fast enough that their full RFC iteration tests take minutes, not half an hour

## Goal

`Curl.Cryptography.UnitLibrary`'s X25519, X448 and CAST-128 run at least five times faster, still constant-time and still bit-exact. Their Integration tests that iterate the RFC vectors in full then finish in minutes, not the 37 minutes they take together today, and every X25519 and X448 key exchange in a TLS, SSH or QUIC handshake gets faster with them.

## Context

- Stewart's request, 2026-10-06: find why the Cryptography Integration tests take 27 minutes and fix it. The new `Integration tests` workflow (`.github/workflows/integration.yml`) runs them on all three platforms on every push, and these tests are almost all of its time.
- Measured 2026-10-06 on Stewart's Windows machine, Release build, `dotnet test -c Release --filter "TestCategory=Integration" --logger trx` (durations from the TRX):
  - `X25519Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`: 27 min 38 s. That is about 1.66 ms per X25519 operation; well-tuned 64-bit implementations take tens of microseconds.
  - `X448Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`: 8 min 15 s, about 0.49 ms per X448 operation.
  - `Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB`: 1 min 42 s for RFC 2144 B.2's million iterations, which rekey on every iteration.
  - The other 23 Integration tests together: under 10 s.
- Likely causes, to confirm with a profile before changing anything:
  - `Field25519` and `Field448` hold an element as 16-bit limbs in `long`s (16 and 28 limbs, TweetNaCl style), so a field multiplication is 256 or 784 limb products plus carries. 51-bit limbs for 2^255-19 (5 limbs, `UInt128` or `Math.BigMul` products) and 56-bit limbs for 2^448-2^224-1 (8 limbs) are the usual fast representations.
  - CAST-128: look at the key schedule (`ApplyKeyScheduleRow`, `KeyScheduleRows`), since B.2 rekeys every iteration, and at any per-call allocation (`subkeys` is a `uint[]` per instance).
- Constraints that do not move:
  - Every member stays constant-time: no branch or index that depends on a secret, conditional moves as masks. The class doc comments say so and `audit-security` checks it.
  - No allocation in the field arithmetic.
  - Base class library only; no intrinsics that do not exist on every platform Curl ships for (x64 and Arm64 on Windows, Linux and macOS) unless a portable path stays beside them.
  - Quality gates at 100% line and branch coverage, complexity at most 10, CRAP at most 30.
- Callers to keep working unchanged: Ed25519 also uses `Field25519`, and X25519 and X448 are reached from TLS (`Curl.Networking`), SSH and QUIC. Their public surface does not change.
- BL-1498 (adversarial tests for this library) depends on this task, so it attacks the faster code.

## Acceptance criteria

- [ ] Notes record a profile, or per-operation timings, of X25519, X448 and CAST-128 before the change, naming where the time went.
- [ ] Every existing test in `Curl.Cryptography.UnitTests` passes unchanged, Integration tests included (`dotnet test Curl.Cryptography.UnitTests -c Release --filter "TestCategory=Integration"`), along with the fast tests of every project that uses the library (`dotnet test --filter "TestCategory!=Integration"`).
- [ ] On the same machine and command as Context, the two `Rfc7748Section52MillionIterations` tests and `EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB` are each at least five times faster than the times in Context. Before and after durations are recorded in Notes.
- [ ] The changed field and cipher code has no secret-dependent branch or table index: each changed class's doc comment states it, and the review stage checks it.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports `Curl.Cryptography.UnitLibrary` at 100% line and branch coverage with no member over complexity 10 or CRAP 30, and `dotnet build -warnaserror` is clean.
- [ ] If a test's own work, not the library, turns out to be a large share of its time, Notes say so and how it was fixed, without dropping or weakening any iteration the RFC specifies.

## Notes

- 2026-10-07, lane 8 (unfinished; the code is in the shift's stash for this task):
  - Before, Release, per operation (C# file-based bench outside the repo, `#:project` on
    the library, HEAD's copy vs the working copy, machine loaded by 8 other lanes):
    X25519 580-824 us, X448 1976-2156 us, CAST-128 rekey + 2 blocks 62-65 us. A Debug
    build of the same bench is 10-20x slower, so bench in Release only.
  - Where the time went: X25519/X448 - every field multiply did 256 / 784 16-bit limb
    products into a stackalloc'd 31 / 55-limb buffer, then cleared and zeroed it; the
    inversions were 254 / 446 square-and-multiply steps (one multiply per bit).
    CAST-128 - each S-box read scans all 256 entries one at a time by mask (ADR-0398);
    B.2 does 640 scans per iteration in the two key schedules and 256 in the four blocks.
  - Done in the working copy: `Field25519` on 5 x 51-bit limbs with 128-bit products
    (`Math.BigMul`), 25 products per multiply and 15 per square, and the standard
    254-squaring / 11-multiply inversion chain; `Field448` on 8 x 56-bit limbs, 64 / 36
    products into a `Span<Int128>`, and an addition-chain inversion (~453 squarings, 13
    multiplies); `Cast128.ReadBox` scans `Vector<uint>.Count` entries per compare
    (`Vector.Equals` mask, `Vector.Sum` of the one kept lane). Callers that wrote the
    second 16-bit limb by hand (X25519's a24, Edwards25519's d) now use
    `SetSmall(uint)`. CLAUDE.md's limb descriptions updated. All 1335 fast tests and all
    5 Integration tests of Curl.Cryptography.UnitTests pass in Release.
  - After, same bench: X25519 285-376 us (2.0-2.2x), X448 744-764 us (2.6-2.8x), CAST
    20.5-20.9 us (3.0-3.1x). Integration TRX (Release, loaded machine, tests in
    parallel): X25519 million iterations 5 m 40 s (27 m 38 s in Context: 4.9x), X448
    14 m 51 s (Context's 8 m 15 s was a quieter machine), CAST B.2 32.9 s (Context 1 m
    42 s: 3.1x).
  - Left: (1) X448 needs another ~2x - the `Span<Int128>` column loop, its Clear and
    ZeroMemory dominate; unrolled locals as in `Field25519`, or Hamburg's Karatsuba on
    the golden-ratio prime, are the next steps. (2) Even X25519's 25-product multiply
    costs ~100 ns here: `Int128` adds and the signed `Math.BigMul`; an unsigned
    `Math.BigMul(ulong, ulong)` path (non-negative limbs, Subtract and Negate adding a
    multiple of p) is the next step. (3) CAST-128's masked scan is near the vector
    width's limit already (32 compares per 256-entry box on AVX2); 5x for B.2 needs fewer
    scans, e.g. one fused pass reading the four key-schedule boxes per row, or a
    bitsliced S-box - or an ADR, under Stewart's delegation, that sets CAST's target at
    what a constant-time scan can reach. (4) Measure-CodeQuality.ps1 not yet run on the
    changed library. (5) The task's timings must be re-taken on an unloaded machine.
- 2026-10-07, lane 1 (unfinished; code again left for the shift's stash, on top of lane 8's):
  - Bench now compares HEAD's sources and the working copy in one run, back to back
    (two throwaway console projects in %TEMP% compiling the library's `*.cs` directly, so
    internals are reachable; best of 5 rounds, Release). The machine is loaded by the
    other lanes, so only the HEAD/WORK ratio in one run means anything; absolute
    microseconds drift by 2x between runs.
  - Found the real cost: `DOTNET_JitDisasm` showed Tier1 `Field448.Multiply` (unrolled
    or as lane 8's loop over a `Span<Int128>`) calling `Int128.op_Addition` 36 times and
    `Math.BigMul(long, long, out long)` 23 times - the method exceeds the JIT's inline
    budget, so the 128-bit arithmetic becomes real calls. Signed `Math.BigMul` is not an
    intrinsic either.
  - Done: `Field448.Multiply` / `Square` now fold and carry 15 column sums each computed
    by a small `Column` / `SquareColumn` method (a loop with public bounds; two inline
    sites, so the JIT inlines them); `FoldAndCarry` and `CarryColumns` are locals, no
    `Span<Int128>` to clear. `Product` in both fields uses the unsigned `Math.BigMul`
    intrinsic plus the two-term signed correction. New `MultiplySmall(result, value,
    small)` in both fields replaces SetSmall + Multiply for a24. X25519 and X448's
    `LadderStep` no longer stackalloc and zero 9 elements per bit: `ScalarMultiply`
    allocates them once beside the ladder (`LadderScratchElements`) and clears them with
    it. All 1335 fast tests of Curl.Cryptography.UnitTests pass.
  - Bench, HEAD vs work, same run: X25519 453-480 vs 132-137 us (3.5x), X448 1698-1739 vs
    613-652 us (2.7x), CAST-128 rekey + 2 blocks 48-52 vs 6.5 us (7.5x - so lane 8's
    3.1x for B.2 in the TRX was machine load or the test's own work; check the test).
  - Left: (1) X448 to 5x: next, give `Field25519.Multiply`/`Square` and the Column
    methods the same JIT-disasm check (`DOTNET_JitDisasm=Multiply`, count `call`s in the
    Tier1 listing), and try Karatsuba on 2^224 (48 products) for X448. (2) Integration
    TRX timings and (3) Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary
    still not run - start them early: the measure takes 30-45 min under load. (4) New
    members `MultiplySmall` need nothing beyond the ladder's tests for coverage, but
    `SquareColumn`'s even/odd branch must show both arms covered (it does by construction:
    columns 0-14).
- 2026-10-07, lane 4 (unfinished; code left for the shift's stash, on top of lane 1's,
  which is stash `fa91580c` - "darkfactory BL-1525 20261007-111121"):
  - **Context swaps the two curve tests' names.** The TRX's `className` shows the
    27-minute test is `X448Tests` (1M x ~1.66 ms) and the 8-minute one `X25519Tests`
    (1M x ~0.49 ms). Targets at 5x are therefore X448 <= 5 m 32 s and X25519 <= 1 m 39 s.
  - Disasm of lane 1's code (`DOTNET_JitDisasm='Multiply Square'`, Tier1): `Field25519`
    called `Product` 24 times per Multiply (not inlined); `Field448.Multiply` and `Square`
    called `Int128.op_Addition` 23 / 33 times and `op_RightShift` 6 / 12 times - once a
    method passes the inline budget, every `Int128` operator is a real call.
    `[AggressiveInlining]` on Column/SquareColumn made X448 slower (1.8 ms vs 1.0 ms).
  - Done: new internal `Accumulator128` (two `ulong` halves, two's complement,
    `MultiplyAdd`, `Add(long)`, `Add(Accumulator128)`, `ShiftRight`, `Low`; carry by bit
    arithmetic, no comparison), every `[AggressiveInlining]`. `Field25519` Multiply/Square
    use `SumOfProducts` (3 and 5 products) into it and `CarryProduct` takes it;
    `Field448`'s Column/SquareColumn/FoldAndCarry/CarryColumns use it. Tier1 disasm now
    shows no call in `Field25519.Multiply`/`Square`. 
  - Bench, HEAD vs work, same run, Release: X25519 805-838 vs 282-292 us (2.9x), X448
    3099-3147 vs 1169-1232 us (2.6x), CAST 55-60 vs 7.9-8.0 us (7x). Field25519.Multiply
    is still 4 KB of code with a 776-byte frame: the `out ulong` low half of
    `Math.BigMul` goes through memory and the 10 accumulators spill. Field448 is ~250 ns
    per operation.
  - Integration TRX, Release, `dotnet test Curl.Cryptography.UnitTests -c Release
    --filter "TestCategory=Integration"`, all 5 pass, run while Measure-CodeQuality and
    other lanes loaded the machine: X448 27 m 23 s, X25519 7 m 21 s, CAST B.2 23.4 s
    (4.4x vs 1 m 42 s). Under this load the curve timings show nothing (the bench ratio
    is 2.6-2.9x); the 5x criterion can only be judged on a machine without 8 lanes.
  - Left: (1) X25519 and X448 to 5x on the bench ratio: the multiply must stop spilling -
    try 64-bit-only arithmetic (radix 2^25.5 x 10 limbs for 25519, 16 x 28-bit limbs for
    448 with Hamburg's Karatsuba), whose products need no 128-bit sum at all, or
    `X86.Bmi2.X64.MultiplyNoFlags` / `ArmBase.Arm64.MultiplyHigh` with the portable path
    beside it. (2) The 5x timings need a quiet machine: run this task interactively or
    with one lane.
  - `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` on this run's code:
    100% line, 100% branch, 0 members over the complexity or CRAP limits (report row
    `| Curl.Cryptography.UnitLibrary | 100 | 100 | 777 | 0 | 10 |`). So the stashed code
    passes every gate but the 5x speed-up; a later change must re-measure.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Partly done, code in the shift's stash: X25519 4.9x, X448 not yet 5x (Field448 needs unrolled limbs), CAST B.2 3.1x (masked scan near its limit); quality measure not run. See Notes.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Partly done, code in the shift's stash: bench vs HEAD X25519 3.5x, X448 2.7x (needs Karatsuba or more JIT inlining work), CAST 7.5x; Integration TRX timings and Measure-CodeQuality not yet run. See Notes.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Partly done, code in the shift's stash: new Accumulator128 removes Int128 calls; bench vs HEAD X25519 2.9x, X448 2.6x, CAST 7x; coverage 100/100, Integration tests pass. Left: X25519/X448 to 5x (64-bit-only limbs or Karatsuba) and timing on an unloaded machine. See Notes.
- 2026-10-07: Backlog -> Doing.
