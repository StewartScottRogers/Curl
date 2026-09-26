// Assembly-level MSTest configuration, shared by every .UnitTests project.
//
// Curl's unit tests are self-contained by design: no shared fixtures, no ambient
// state, no network, and time arrives through an injected TimeProvider rather than
// the clock. Running them in parallel is therefore safe, and across 24 test projects
// it is the difference between a test run you wait for and one you do not.
// Workers = 0 means one worker per processor.
//
// MSTest's MSTEST0001 analyzer requires this decision to be explicit, and warnings
// are errors here. The decision is identical for all 24 projects, so it is made once
// in this file and linked into each of them by Directory.Build.props - rather than
// copied 24 times, where the copies would drift.
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
