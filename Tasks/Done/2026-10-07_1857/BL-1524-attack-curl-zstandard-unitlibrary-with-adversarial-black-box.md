---
id: BL-1524
title: Attack Curl.Zstandard.UnitLibrary with adversarial black-box tests in Curl.Zstandard.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1490]
touches: [Curl.Zstandard.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1524 — Attack Curl.Zstandard.UnitLibrary with adversarial black-box tests in Curl.Zstandard.UnitTests

## Goal

`Curl.Zstandard.UnitTests` gains adversarial black-box tests that attack `Curl.Zstandard.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1490.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Zstandard.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: frame headers with a wrong magic number, window sizes at and past the limit, blocks at 128 KiB and one past, corrupted Huffman and FSE tables, checksum mismatches, skippable frames, unknown dictionary IDs, truncated input, and decompression bombs whose expansion ratio is extreme.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Zstandard.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Zstandard.UnitTests -warnaserror` is clean and `dotnet test Curl.Zstandard.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Zstandard.UnitTests/` and this task file.

## Notes

- Public surface attacked: `ZstandardDecoder` (constructor, `Decompress`, `TryDecompress`, `LastError`), `XxHash64.Hash` and `XxHash64Accumulator`; every other type in the library is internal. Oracle: RFC 8878 and the decoder's documented contract (a named `ZstandardDecodeError`, never an exception, a hang, or content other than the frame's). No real curl measurement was needed: nothing new is visible on the command line, and the existing measured frames were reused.
- Boundaries (applied): `TryDecompress_FieldExactlyAtItsLimit_WritesTheContent` (block exactly the 1 KiB window, raw block exactly 128 KiB, 2-byte content size at 256 and 65791, single-segment 0-byte frame, empty compressed block, 4-byte zero dictionary ID, skippable frames of all 16 nibbles), `Decompress_FieldOnePastItsLimit_IsInvalidDataWithTheNamedError` (raw and compressed block at 128 KiB + 1, content one short, 0-byte single-segment frame given a byte, window one step past 2^31, largest window descriptor, 2^28 at the default limit, single-segment `ulong.MaxValue` content size, `ulong.MaxValue` content size given one byte, largest and smallest non-zero dictionary IDs), `Constructor_MaxWindowLogExactlyAtItsLimit_DecodesAOneKibibyteWindowFrame`, `Decompress_EmptySourceAndDestinationOnANewDecoder_NeedsMoreDataWithoutFailing`, `Decompress_EmptyDestinationRepeatedlyInsideABlock_IsDestinationTooSmallUntilRoomIsGiven`, `Decompress_SkippableFrameDeclaringUIntMaxValueBytes_NeedsMoreDataAndTryDecompressReturnsFalse`, `Append_EveryLengthUpTo130InRandomPieces_GivesWhatHashGives` (seeds 0, 1, `ulong.MaxValue`).
- Malformed input (applied): `Decompress_EveryProperPrefixOfALibzstdFrame_NeedsMoreDataAndNeverFails`, `TryDecompress_EveryProperPrefixOfAFrameWithAChecksum_ReturnsFalse`, `TryDecompress_EverySingleBitFlipOfAFrameWithAChecksum_NeverThrowsAndNeverYieldsOtherContent`, `TryDecompress_RandomBitFlipsInACompressedLibzstdFrame_NeverThrowAndNeverYieldOtherContent` (seeded; corrupts Huffman and FSE tables, sequences and checksums in real libzstd frames), `Decompress_RandomCompressedBlockBodies_NeverThrowAndNameEveryRefusal` (seed 1524, 500 random block bodies), and the decompression bombs `Decompress_DecompressionBombOf400RleBlocks_StreamsFiftyMebibytesInBoundedMemory` (about 1.6 KB in, 50 MiB out through a 64 KiB destination, under 4 MiB allocated) and `Decompress_TinyFrameDeclaringTheLargestDefaultWindow_AllocatesOnlyWhatItUses`.
- Invalid partitions (applied): `Decompress_InvalidPartition_IsInvalidDataWithTheNamedError` - reversed magic, magic one below Zstandard's, skippable magic with a wrong high byte, reserved bit with every descriptor bit set, checksum with every byte wrong, checksum of empty content, reserved block type after a good block, compressed block of all 0xFF, garbage where the next frame's magic should be; plus `Hash_SameInputUnderDifferentSeeds_GivesDifferentHashes`.
- State and concurrency (applied): `Decompress_FrameSplitIntoTwoCallsAtEveryOffset_WritesTheSameContent`, `Decompress_AfterInvalidDataThenAValidFrame_StaysFailedAndKeepsTheFirstError`, `Decompress_OneDecoderReusedAcrossDifferentLibzstdFrames_WritesEachFramesContent`, `Decompress_TwoDecodersFedAlternateBytes_DoNotDisturbEachOther`, `TryDecompress_SixteenTasksAtOnce_EachWritesTheSameContentAsOneAlone`, `GetCurrentHash_CalledTwiceThenAppendedTo_DoesNotDisturbTheHash`, `Reset_MidStripe_GivesWhatAFreshAccumulatorUnderTheSameSeedGives`, `Append_ManyEmptySpans_LeavesTheHashOfNothing`, `Hash_SixteenTasksAtOnce_EachGivesTheSameHash`. Time and cancellation do not apply: the library takes no `TimeProvider` and no token.
- Defects found: none. Every attack passed against the library as it is, so no follow-up task was filed.
- No input is over 1 MiB (the largest is the 128 KiB raw block), so no test needed `TestCategory("Integration")`; the bomb's 50 MiB of output is streamed through a 64 KiB buffer, never held.
- Test count: 276 before, 336 after (`dotnet test Curl.Zstandard.UnitTests --filter "TestCategory!=Integration"`). `dotnet build Curl.Zstandard.UnitTests -warnaserror` is clean and `dotnet format --verify-no-changes` passes on the project.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Zstandard.UnitTests attacks the decoder and XXH64 at boundaries, with malformed frames, invalid partitions, bombs and concurrency; 336 tests, no defects found
