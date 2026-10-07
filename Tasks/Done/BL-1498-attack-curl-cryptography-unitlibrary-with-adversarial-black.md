---
id: BL-1498
title: Attack Curl.Cryptography.UnitLibrary with adversarial black-box tests in Curl.Cryptography.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1464, BL-1525]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1498 — Attack Curl.Cryptography.UnitLibrary with adversarial black-box tests in Curl.Cryptography.UnitTests

## Goal

`Curl.Cryptography.UnitTests` gains adversarial black-box tests that attack `Curl.Cryptography.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1464.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Cryptography.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: the hand-built primitives as black boxes: known-answer vectors at lengths 0, 1, block-1, block and block+1; all-zero and all-0xFF keys; wrong key, nonce and tag lengths; a single flipped bit in every byte of a tag or ciphertext; Ed25519 non-canonical encodings and small-order points; X25519 low-order public keys; big-integer edge values (0, 1, p-1, p). Coordinate with the audit office's fuzzing (`audit-security`) rather than repeat it.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Cryptography.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.

## Notes

- New tests: `Curl.Cryptography.UnitTests/CryptographyAdversarialTests.cs`, one class for
  the whole attack (it spans many primitives, so it is named for the attack rather than
  one production class). Fast-run test count before: 1335; after: 1445 (+110 cases).
- Oracle: RFC 7748 (X25519 top-bit masking, non-canonical u processed mod p), RFC 8032
  (S < L, y < p and no x = 0 with the sign bit), RFC 8439 (AEAD tag), and each type's
  documented exceptions. No command-line behaviour is involved, so no curl measurement.
- Family 1, boundaries: `AeadChaCha20Poly1305_PlaintextAtBlockBoundary_RoundTripsAndRejectsATamperedTag`
  (0, 1, 63, 64, 65, 129), `AeadChaCha20Poly1305_AllZeroOrAllOnesKey_RoundTrips`,
  `X25519_PeerKeyZeroOrP_ReturnsFalseWithAnAllZeroSecret` (big-integer 0 and p),
  `Cipher_KeyLengthAtTheEdgeOfItsRange_IsAcceptedOnlyInsideIt` (RC4, Blowfish, CAST-128,
  DES, Camellia, ARIA, AES-CTR), `Rc4_DiscardKeyStreamZeroOrNegative_ChangesNothingOrThrows`,
  `Md4AndRipemd160_InputFedInTwoPiecesAtEverySplit_EqualsTheOneShotHash` (0, 1, 55, 56,
  63, 64, 65, 119, 120), `Shake_OutputReadInPiecesOfEachSize_EqualsOneRead` (rate - 1,
  rate, rate + 1 for both rates).
- Family 2, malformed input: `AeadChaCha20Poly1305_SingleBitFlippedInEveryByte_IsRejectedWithAnAllZeroPlaintext`
  (tag, ciphertext, associated data), `Poly1305_Verify_SingleBitFlippedInEveryTagByte_ReturnsFalse`,
  `HmacRipemd160_Verify_SingleBitFlippedInEveryMacByte_ReturnsFalse` (keys 0, 64, 65 bytes),
  `Ed25519_Verify_SingleBitFlippedInEverySignatureByte_ReturnsFalse`,
  `Ed25519_Verify_NonCanonicalSPlusGroupOrder_ReturnsFalse`,
  `Ed25519_Verify_NonCanonicalIdentityPublicKey_ReturnsFalse` (y = p + 1; x = 0 with sign bit),
  `X25519_PeerKeyWithTheTopBitSet_GivesTheSameSecretAsWithItClear`,
  `X25519_NonCanonicalPeerKeyPPlusNine_GivesTheSameSecretAsNine`,
  `X448_NonCanonicalPeerKeyPPlusFive_GivesTheSameSecretAsFive`. X25519/X448 low-order
  keys are already pinned by `X25519Tests`/`X448Tests`, so not repeated.
- Family 3, invalid partitions: `AeadChaCha20Poly1305_SpanOfTheWrongLength_ThrowsArgumentException`,
  `Poly1305_SpanOfTheWrongLength_ThrowsArgumentException`, `X25519_SpanOfTheWrongLength_ThrowsArgumentException`,
  `X448_SpanOfTheWrongLength_ThrowsArgumentException`, `Ed25519_SpanOfTheWrongLength_ThrowsArgumentException`,
  `BlockCipher_BlockOfTheWrongLength_ThrowsArgumentExceptionBothWays`,
  `BlowfishAndCast128Cbc_PartialBlockOrShortVector_ThrowsArgumentException`,
  `Hash_DestinationOneByteOff_ThrowsArgumentException` (MD4, RIPEMD-160, HMAC-RIPEMD-160, SHA-3).
- Family 4, state and concurrency: `Rc4AndAesCtr_KeyStreamSplitAtEveryOffset_EqualsOneCall`,
  `Shake_AppendAfterReadThenReset_RefusesThenStartsAgain`,
  `DisposablePrimitive_UsedAfterDispose_ThrowsObjectDisposedException` (twelve types, double
  Dispose), `Ed25519AndX25519_CalledFromManyThreadsAtOnce_GiveTheSameAnswersAsOneAfterAnother`.
  The split-at-every-offset hash test also exercises instance reuse after GetHashAndReset.
- Not attacked here, by choice: ML-KEM, ML-DSA, sntrup761, HPKE, Brainpool, DSA, RSA and
  finite-field DH, whose malformed-encoding attacks overlap `audit-security`'s fuzzing;
  their wrong-length partitions are already pinned in their own test classes.
- Defects found: none; every attack passed, so no follow-up task was filed. No test
  takes an input over 1 MiB, so none is Integration.

## Log

- 2026-10-06: Created.
- 2026-10-06: Now depends on BL-1525, which speeds up X25519, X448 and CAST-128, so the attacks run against the final code.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Cryptography.UnitTests attacks the library with 110 adversarial black-box cases across all four families; no defects found
