---
id: BL-1518
title: Attack Curl.Protocol.Ssh.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ssh.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1484]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1518 — Attack Curl.Protocol.Ssh.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ssh.UnitTests

## Goal

`Curl.Protocol.Ssh.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Ssh.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1484.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Ssh.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: the binary packet protocol (packet lengths at the minimum, 35000 and past, padding-length rules, a single flipped MAC bit), KEXINIT name-lists that are empty, huge or with stray commas, `mpint` encodings with extra leading zeros or a negative sign, `known_hosts` lines that are malformed, and SFTP status codes and handle lengths at their limits. Point at BL-1285's audit fuzzer work rather than repeat it.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Ssh.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.

## Notes

- Surface attacked: the library's public types only - `SshWireDecoders` (the raw-byte
  readers BL-1285's audit fuzzer drives; these tests make its kind of attack permanent
  rather than repeat its fuzzing) and `SshProtocolHandler` behind a scripted
  `IConnector`. The rest of the library is `internal`. Two new files:
  `SshWireDecodersAdversarialTests.cs` (112 cases) and
  `SshProtocolHandlerAdversarialTests.cs` (17 cases). Oracle: RFC 4253 and libssh2
  1.11.1's limits as the library documents them; the handler tests pin only curl's exit 2
  and its "Failure establishing ssh session: " prefix, which real curl prints for every
  libssh2 handshake failure, not a guessed libssh2 reason.
- **Boundaries:** `CountWholePacketsAsync_PacketAtTheLargestLength_IsRead` (39996),
  `..._PacketOneBlockPastTheLargestLength_IsRefused`, `..._PacketAt35000Bytes_...`,
  `..._PacketLengthFarPastTheMaximum_...` (0xFFFFFFFF, 0x80000000, 0x7FFFFFF8),
  `..._SmallestWholePacket_IsRead`, `..._BlockAlignedPacketTooShortForAPayload_IsRefused`,
  `..._PaddingLengthAtEachEdgeOfTheValidRange_...` (7 rows),
  `TryDecodeSftpAttributes_EachFieldExactlyWholeAndOneByteShort_...`,
  `..._SizeOfUInt64Maximum_ReturnsTrue`, `TryInflatePayload_PayloadOfExactlyTheLargestSize_ReturnsTrue`,
  `..._PayloadOneBytePastTheLargestSize_ReturnsFalse`, `..._BombInflatingToTenMebibytes_ReturnsFalse`
  (the bomb is ~10 KiB compressed, so it stays a fast test),
  `SshProtocolHandlerAdversarialTests.ExecuteAsync_FirstPacketLengthOutsideTheValidRange_...`.
- **Malformed input:** `..._TwoPacketsCutAtEveryOffset_...`, `..._SeededRandomBytes_NeverThrow`
  (seed 1518), KEXINIT empty / stray-comma / 10000-name / non-ASCII name-lists, name-list
  lengths past the payload, cut at every offset, trailing bytes; SFTP extended counts and
  lengths past the data; host keys and signatures for 8 algorithms cut at every offset,
  one bit flipped, seeded random byte changes (seed 4253), a negative RSA exponent mpint,
  extra leading zeros in RSA mpints, malformed and off-curve ECDSA points, a curve name
  mismatch, algorithm names one character off; zlib with a bad header check and seeded
  random bytes (seed 1950); handler: KEXINIT cut inside its packet, malformed server
  identifications.
- **Invalid partitions:** padding below the minimum / leaving no payload / past the packet,
  packet lengths not block-aligned, unknown SFTP flag bits, empty host key for every
  algorithm; handler: a KEXINIT nothing agrees with (empty, only commas) and
  `ExecuteAsync_MessageValidOnlyAfterKeyExchangeSentBeforeIt_...` (52, 94, 21, 31 before KEXINIT).
- **State and concurrency:** `CountWholePacketsAsync_SameBytesOnSixteenTasksAtOnce_...`,
  `TryInflatePayload_SameFirstPacketTwice_...`, `TryDecodeKexInit_RefusedInputThenAWholeOne_...`,
  `TryDecodeHostKeySignature_EveryAlgorithmOnManyTasksAtOnce_...`; handler:
  `ExecuteAsync_BannerAndPacketsDeliveredOneByteAtATime_EndTheSameAsDeliveredWhole`,
  `ExecuteAsync_SameHostileServerOnEightTransfersAtOnce_EveryOneFailsTheSame`. Time and
  cancellation are not applicable at this surface: `SshWireDecoders` reads an in-memory
  array that never waits, and the handler's timeouts belong to the connector.
- **Defects found: none.** Every attack was refused by return value or exit 2; nothing
  threw, hung or allocated past its limit, so no follow-up task was filed. One behaviour
  noted, not a defect: a host certificate is read only as far as its certified key (name,
  nonce, key fields), as libssh2 1.11.1 reads one, so a certificate cut after its key still
  decodes; the cut-at-every-offset test covers offsets before the key's end.
- Nothing over 1 MiB is held by a fast test (the largest input is ~40 KB), so no
  Integration test was needed. No drive-letter paths or platform text.
- Test count, `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"`:
  1673 before, 1802 after. `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror`: 0 errors.
  The library is unchanged, so its coverage is unchanged and Measure-CodeQuality was not run.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Protocol.Ssh.UnitTests attacks SshWireDecoders and SshProtocolHandler with 129 adversarial black-box tests; no defects found
