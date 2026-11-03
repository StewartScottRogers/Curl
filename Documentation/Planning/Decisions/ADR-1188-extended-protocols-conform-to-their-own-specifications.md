# ADR-1188: Extended protocols conform to their own specifications

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4503 (split from BL-4462)
- Decided by Claude under Stewart's delegation.

## Context

Curl's extended protocols (ADR-0999, ADR-1177) have no upstream curl to compare against: curl
does not speak them. BL-4462 planned a conformance harness for them and 20 per-protocol tasks that
measure each one; ADR-1185 records how the harness runs curl and ADR-1186 how requirements are
checked. This ADR records the rules the per-protocol tasks are written against.
`Documentation/Conformance/Extended/README.md` states them for those tasks.

## Decision

- **Measured against the specification, not curl.** An extended protocol's conformance cases run a
  real curl command line in process (`ExtendedConformanceHarness.RunAsync`) against a reference
  server and check the bytes curl sends, its output and its exit code against what the protocol's
  own specification requires. Where curl's generic behaviour applies (option parsing, exit codes such
  as 28 for `-m`), curl's behaviour stays the reference.
- **Time is injected.** A run takes a `TimeProvider` (BL-4503) and hands it to
  `InProcessCurl.RunAsync`'s overload that takes one (BL-4500), so every timer of the run, `-m`'s
  among them, runs on it and a timeout case needs no real wait. The `InProcessCurlRunner` delegate
  takes the clock, so the tests' method group binds to that overload; `TimeProvider.System` when the
  case gives none.
- **Extension isolation.** `VerifyExtensionIsolation` lets only the hosts and the extensions
  reference an extension. `Curl.Conformance.Extended.UnitTests` references the hosts `Curl.Console`
  (which reaches every extension through `Curl.Extension.Composition`) and `Curl.Mcp`, and hands the
  harness `InProcessCurl.RunAsync`; `Curl.Conformance.Extended.UnitLibrary` references only
  `Curl.Protocol.Abstractions`, `Curl.Networking`, `Curl.Conformance.UnitLibrary` and the shared
  layers, never an extension. `Directory.Build.props` is unchanged.
- **Reference servers do not reuse client codecs.** A reference server implements its protocol's
  wire format from the specification on its own. A server built on the client library's encoder and
  decoder would agree with every bug in them, and the case would pass while curl is wrong.
- **IDs.** A requirement ID is `<Name>:<source>-<locator>`, using the specification's own normative
  IDs where it has them (`Mqtt5:MQTT-3.1.2-3`); a test carries `[SpecRequirement("<id>")]`.
- **Matrices.** `Documentation/Conformance/Extended/<Name>.md` (or `<Name>-<Part>.md`) is one table
  `| ID | Level | Requirement | Covered by |`, Covered by naming the test's `Class.Method` or
  `n/a: <reason>`. Every row names a test carrying its ID, every carried ID is a row, no ID is a row
  twice, and a matrix has rows. The folder's `README.md` is not copied as a matrix.
- **Known failures.** `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` lists
  `<requirement id> BL-####` per line, naming the filed task that fixes the case; a listed case passes
  while failing and fails once it passes; a listed ID no test carries fails the list's check.

## Consequences

Each per-protocol task adds a reference server, its cases and its matrix, and edits no registry.
Writing a reference server from the specification costs more than reusing the client's codec, and
two independent readings of the specification can still agree on the same mistake; the matrix ties
each case to the text it reads, which is where such a mistake is found.
