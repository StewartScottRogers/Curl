# ADR-0421 — Integration tests live only in `Curl.<Area>.IntegrationTests` projects

- **Status:** Accepted
- **Date:** 2026-10-07
- **Task:** BL-1598

The rule itself is Stewart's, approved on 2026-10-07. Decisions 1-4 below are decided by
Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"): the first three while
planning the work on 2026-10-07, the fourth in BL-1598.

Supersedes in part ADR-0083 (its fast tests that open local sockets are named as unit tests
by decision 4, and its Integration tests now live in `Curl.Networking.IntegrationTests`) and
ADR-0118 (its "Tests" section's "Vectors that take more than a second (RFC 7748's million
iterations) are marked `TestCategory=Integration`", replaced by decision 3).

## Context

Until 2026-10-07 Integration tests sat beside fast ones in the `*.UnitTests` projects,
told apart only by `[TestCategory("Integration")]`. Nothing said what made a test an
Integration test, so the category was used for two different things: tests that touch the
real world (loopback round trips, the Windows `NUL` device, extended attributes, the SSH
agent and Pageant) and tests that are merely slow (the RFC 7748 million-iteration vectors).
Stewart moved `Curl.Networking.UnitTests`' Integration tests into a new
`Curl.Networking.IntegrationTests` project (`6ecb2781f`, made buildable by BL-1597) and
approved the rule below.

## Decision

### The rule (Stewart, 2026-10-07)

- An **Integration test** touches something real outside the process: a socket, the disk,
  the OS, a native API, a system agent. It carries `[TestCategory("Integration")]`.
- Integration tests live only in projects named `Curl.<Area>.IntegrationTests`. A
  `*.UnitTests` project contains no `[TestCategory("Integration")]` test. Every test in an
  `*.IntegrationTests` project carries the Integration category, so
  `dotnet test --filter "TestCategory!=Integration"` still skips all of them.

### 1. Sort order: the IntegrationTests project sits immediately before its library

`Curl.<Area>.IntegrationTests` sorts alphabetically before `Curl.<Area>.UnitLibrary`, so
"library, then its IntegrationTests, then its UnitTests" cannot happen in the flat
alphabetical `Curl.slnx`. The names stay as they are, and the naming rule says the
IntegrationTests project sits in the flat run immediately before its library. For
`Curl.Console`, it sits immediately before `Curl.Console.UnitTests`, which already sorts
before `Curl.Console`. In the tree today: `Curl.Networking.IntegrationTests`,
`Curl.Networking.UnitLibrary`, `Curl.Networking.UnitTests`.

**Why:** renaming the projects to force a different order (`Curl.<Area>.ZIntegrationTests`,
or `UnitIntegrationTests`) would make the name say something other than what the project
is. The alphabetical run already keeps the three projects of an area together.

### 2. CI needs no change

Measured on 2026-10-07 with .NET SDK 10.0.401 and MSTest 4.4.1, `dotnet test` in VSTest
mode: `dotnet test Curl.Protocol.Dict.UnitTests --no-build --filter "TestCategory=NoSuchCategoryXyz"`
prints `No test matches the given testcase filter` and exits 0. So:

- `.github/workflows/ci.yml`'s `dotnet test -c Release --no-build --filter "TestCategory!=Integration"`
  passes over an `*.IntegrationTests` project whose tests are all filtered out.
- `.github/workflows/integration.yml` runs `dotnet test -c Release --no-build --filter "TestCategory=Integration"`
  against the whole solution, so it picks up every new `*.IntegrationTests` project in
  `Curl.slnx` without an edit.

**Why:** `ci.yml` is a guard file that changes only through the `audit` branch; the
measurement shows no task needs to touch it. (CI run 37650397456 failed on `6ecb2781f` for
a different reason, a duplicate `Parallelize` attribute, fixed by BL-1597.)

### 3. Long-running tests that touch nothing outside the process are LongRunning, not Integration

A test that is slow but pure computation stays in its `*.UnitTests` project, loses the
Integration tag, and carries `[TestCategory("LongRunning")]` plus a custom MSTest condition
attribute (derived from `ConditionBaseAttribute`, as `OSCondition` is) that skips it unless
the environment variable `CURL_RUN_LONG_RUNNING_TESTS` is `1`. The fast command stays
`dotnet test --filter "TestCategory!=Integration"`, where these tests show as skipped;
`integration.yml` (not a guard file) runs them with the variable set:
`CURL_RUN_LONG_RUNNING_TESTS=1 dotnet test --filter "TestCategory=LongRunning"`. A
long-running test that drops under 3 seconds in a Debug build loses both attributes and
joins the fast run.

The three such tests today are in `Curl.Cryptography.UnitTests`, each tagged
`[TestCategory("LongRunning")]` and `[RunsOnlyWhenLongRunningTestsAreEnabled]` (the
condition attribute, in `RunsOnlyWhenLongRunningTestsAreEnabledAttribute.cs`):
`X25519Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`,
`X448Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK` and
`Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB`.
A fourth candidate,
`BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP384r1AndP512r1Vector_GivesItsExpectedResult`,
measured under 3 seconds per row in a Debug build when BL-1604 retagged the others
(2026-10-07: P-384r1 about 1 s, P-512r1 about 2 s), so by the rule above it carries
neither attribute and runs in the fast run.

**Why:** they cannot join the fast run - BL-1525's partial speed-up measured 5 min 40 s
(X25519), 14 min 51 s (X448) and 32.9 s (CAST-128 B.2) on 2026-10-07, against
`TestDiagnostics`' 3-second `SLOW:` budget (ADR-0417) - and a
`Curl.Cryptography.IntegrationTests` project for them would contradict the rule's
definition. A new category excluded by the fast filter would change the fast command, and
with it `ci.yml`, `RunDarkFactory.ps1`, `Measure-CodeQuality.ps1`, the `verify` skill and
hundreds of task criteria that quote it; a condition attribute changes none of them.

### 4. Test-owned scratch files and unconnected loopback sockets are unit tests

The rule's "the disk" and "a socket" mean state or a party the test does not own. A test
stays a unit test, untagged, in its `*.UnitTests` project when everything real it touches
is scratch it creates and removes itself:

- files and directories inside a temporary directory the test creates and deletes; and
- a loopback socket it opens, binds, listens on or closes without sending or receiving a
  byte (ADR-0083's "fast tests that open local sockets without sending anything").

A test is an Integration test when it depends on anything else: bytes crossing a socket
(a loopback round trip included), a file or device it did not create (the Windows `NUL`
device, the repository's own source tree, a user's `.netrc`), file-system features that
vary by machine (extended attributes), a native API, or a system agent (the SSH agent,
Pageant).

So `Curl.Core.UnitTests/FileSystem/PhysicalFileSystemTests.cs` (bar its `NUL` test, moved
by BL-1601), `Curl.Console.UnitTests/PhysicalOutputPathsTests.cs`,
`DiskWriteOutFileOpenerTests.cs`, `KerberosDiskFileReaderTests.cs`,
`KerberosDiskFileWriterTests.cs`, and `Curl.Networking.UnitTests`' socket-opening tests
stay where they are, untagged, and no move task is filed for them.

**Why:** these tests are as deterministic and platform-neutral as an in-memory test, need no
network, service or machine state, and run in milliseconds. They are the only tests that
reach the thin adapters (`PhysicalFileSystem`, the disk openers, the socket constructors and
bind paths). Moving them out of the fast run would leave those adapters at 0% on the
measured run, and the only ways back to 100% - a seam whose default implementation is the
same disk or socket call, or `[ExcludeFromCodeCoverage]` on real code - are the ones
ADR-0083 already weighed and limited to three members, because the first only moves the
uncovered lines and the second stops measuring code that tests can in fact reach. Reading
the repository's source tree is not scratch - it depends on the checkout - which is why
BL-1603 checks the rule at build time rather than in a test.

## Consequences

- Root `CLAUDE.md` ("Repository layout", "Project naming", "Build and test commands",
  "Quality gates"), `Documentation/Wiki/Glossary.md`, `Documentation/Product/Product-Overview.md`,
  `MSTestSettings.cs`, the `new-project` and `verify` skills and the `coverage-auditor`
  agent state the rule (BL-1598).
- Coverage is measured from the fast run only. Moving a test into an `*.IntegrationTests`
  project never changes a library's measured coverage, and a line only an Integration test
  reaches is uncovered unless ADR-0083's exclusion names it.
- The moves, each its own task: BL-1599 (`Curl.Cli`), BL-1600 (`Curl.Console`), BL-1601
  (`Curl.Core`), BL-1602 (`Curl.Protocol.Ssh`). `Curl.Networking` was moved by Stewart in
  `6ecb2781f` and made buildable by BL-1597. BL-1600 moves `CurlCompositionTests`' two
  temp-directory `-w %output{}` tests as filed, already tagged Integration; by decision 4
  they could return to the fast run untagged, which is a later choice, not a requirement.
- BL-1603 fails the build when a `*.UnitTests` project holds an Integration test or an
  `*.IntegrationTests` project holds a test without the category.
- BL-1604 retagged three of the four slow Cryptography tests as LongRunning and returned
  the Brainpool test, by then under 3 seconds, to the fast run untagged (decision 3).
- `Measure-CodeQuality.ps1` (BL-1605), `RunDarkFactory.ps1` (BL-1606) and
  `Audit/Tools/Find-WeakTests.ps1` (BL-1607, interactive only) learn the new projects.

## Alternatives considered

- **Keep Integration tests in `*.UnitTests` projects, told apart by category only.** Lost:
  it is what let slow tests and real-world tests share one category, and it makes "this
  project is fast" unknowable from its name.
- **Exclude a new `LongRunning` category in the fast filter.** Lost to decision 3's reason:
  the fast command is quoted by guard files, scripts and hundreds of tasks.
- **Count every temporary file and every opened socket as Integration.** Lost to decision 4's
  reason: it takes the thin adapters off the measured run for no gain in determinism.
