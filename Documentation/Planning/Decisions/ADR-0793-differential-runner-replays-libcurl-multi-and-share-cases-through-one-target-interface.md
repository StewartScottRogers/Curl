# ADR-0793: The differential runner replays Libcurl multi and share cases through one target interface

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3238
- Decided by Claude under Stewart's delegation.

## Context

BL-3238 ports the tester's `LibTest` multi and share modes (`MultiMode.cs`, `ShareMode.cs`) and
their inputs (`m_in.json`, `m2.json`, `m2i.json`, `m3.json`, `sh.json`) into the differential
runner, replayed against Curl's Libcurl and the reference libcurl 8.22.0 loaded as in ADR-0792.

## Decision

- `Curl.Conformance.Differential.UnitLibrary/Libcurl/Multi` holds everything that needs no native
  library: `LibcurlMultiCaseReader` (JSON with `JsonDocument`), the `ILibcurlMultiTarget` calls a
  case makes, and `LibcurlMultiReplayer`, which writes the tester's tokens (`add=`, `run=`,
  `info=[...]`, `b{i}=hex/code`, `poll=`, `runfor=`, `sset{id}=`, `perf{i}=`). One replayer
  serves both libraries, so the line format cannot drift between them, and it is unit tested
  with a recording fake and a stepped `TimeProvider`.
- `Curl.Conformance.Differential.IntegrationTests/LibcurlMulti` holds the two targets
  (`CurlLibcurlMultiTarget`, `ReferenceLibcurlMultiTarget` over `ReferenceLibcurlMultiLibrary`'s
  function pointers) and `LibcurlMultiDifferentialTests`, which writes `multi_report.txt` in the
  easy report's shape (`LibcurlEasyReportWriter`), one block per case file.
- A step that names an easy handle its case already cleaned up (`m_in` case 12: `cleanup_easy`
  then `add`) is written `freed` without a call on either side: passing a freed handle to the
  reference libcurl is undefined behaviour and can crash the test host.
- `info=[...]` is sorted ordinally, not with the current culture as `MultiMode` did, so the line
  does not depend on the machine's culture.
- The tester's `mi.json` is a mime input (`mo.txt` holds mime tokens), not a multi one; it stays
  with the mime replay (BL-3239).

## Consequences

The order in which concurrent loopback transfers finish (the `q` numbers in `info=[...]`) varies
from run to run in both libraries, so those differences are noise, as in the tester's own report.
