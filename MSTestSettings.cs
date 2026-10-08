// Assembly-level MSTest configuration, shared by every .UnitTests and
// .IntegrationTests project.
//
// Curl's unit tests (*.UnitTests) are self-contained by design: no shared fixtures, no
// ambient state, no network, and time arrives through an injected TimeProvider rather
// than the clock. Integration tests, which touch a socket, the disk, the OS, a native
// API or a system agent, live only in *.IntegrationTests projects (ADR-0421) and each
// uses resources of its own. Running either kind in parallel is therefore safe, and
// across every test project it is the difference between a test run you wait for and
// one you do not. Workers = 0 means one worker per processor.
//
// MSTest's MSTEST0001 analyzer requires this decision to be explicit, and warnings
// are errors here. The decision is identical for every test project, so it is made
// once in this file and linked into each of them by Directory.Build.props - rather
// than copied into each, where the copies would drift.
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
