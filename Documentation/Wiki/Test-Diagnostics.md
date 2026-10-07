# Test diagnostics

Every unit test writes labelled lines to its `TestContext` output, so an AI reading the
log of a failed or slow test can debug it, or see where its time went, without running
it again (BL-1457, [ADR-0417](../Planning/Decisions/ADR-0417-every-unit-test-writes-timing-and-a-slow-line-through-shared-test-diagnostics.md)).

The code is one root file, `TestDiagnostics.cs`, which `Directory.Build.props` links into
every `.UnitTests` project beside `MSTestSettings.cs`. See the lines with
`dotnet test <project> --logger "console;verbosity=detailed"`, or in CI with
`gh run view <id> --log-failed`.

## Lines every test writes

MSTest's global test hooks (`TestDiagnosticsHooks`) write these with no change to the test:

| Line | Example |
| --- | --- |
| `START` | `START Curl.Core.ByteRangeParserTests.TryParse_FirstAndLast_IsBounded (TryParse_FirstAndLast_IsBounded ("0-4",0,4))` - the display name in brackets only when it differs from the method name (data rows). |
| `END` | `END Curl.Core.ByteRangeParserTests.TryParse_FirstAndLast_IsBounded: Passed in 11 ms (arrange 0, act 0, assert 0)` - the outcome, whole milliseconds, and how many ARRANGE, ACT and ASSERT or DIFF lines the test wrote. |
| `SLOW:` | `SLOW: Curl.Core.SampleTests.Sample took 3001 ms (budget 3000 ms)` - after `END`, only when the test took more than `TestDiagnostics.SlowTestBudgetMilliseconds` (3000 ms, Stewart's budget). |

## Lines a test writes itself

Get the test's diagnostics from its `TestContext`, then label what matters:

```csharp
using Curl.Testing;

public TestContext TestContext { get; set; } = null!;

[TestMethod]
public async Task Run_FileUrl_WritesBody()
{
    var diagnostics = TestDiagnostics.For(TestContext);
    diagnostics.Arrange("command line", "curl file:///dir/x");

    int exitCode;
    using (diagnostics.Phase("transfer"))
    {
        exitCode = await RunAsync("file:///dir/x");
    }

    diagnostics.Act("exit code", exitCode);
    diagnostics.Bytes("stdout", stdout);
    diagnostics.Diff("stdout", expected, stdout);
    diagnostics.Assert("exit code", 0, exitCode);
    Assert.AreEqual(0, exitCode);
}
```

| Method | Line | Example |
| --- | --- | --- |
| `Arrange(label, value)` | `ARRANGE <label>: <value>` - an input that matters. | `ARRANGE command line: curl file:///dir/x` |
| `Act(label, value)` | `ACT <label>: <value>` - a result. | `ACT exit code: 0` |
| `Assert(label, expected, actual)` | `ASSERT <label>: expected <e>, actual <a>` | `ASSERT exit code: expected 0, actual 0` |
| `Bytes(label, bytes)` | `BYTES <label> (<n> bytes): <hex> \| <text>` - non-printable bytes as `.`; past 256 bytes (`ByteDisplayCap`) ` ... (<m> more bytes)`. | `BYTES request (7 bytes): 47 45 54 0d 0a 7f ff \| GET....` |
| `Diff(label, expected, actual)` (bytes) | The first difference, the lengths, or equal. | `DIFF body: first difference at byte 2: expected 0x63 'c', actual 0x0a '.'`<br>`DIFF body: lengths differ, expected 3 bytes, actual 2 bytes, equal for the first 2`<br>`DIFF body: equal (3 bytes)` |
| `Diff(label, expected, actual)` (strings) | The same by character. | `DIFF stderr: first difference at character 1: expected 0x62 'b', actual 0x09 '.'` |
| `Phase(name)` | `PHASE <name>: <n> ms`, when the returned scope is disposed. | `PHASE handshake: 42 ms` |

`ARRANGE`, `ACT`, and `ASSERT` and `DIFF` lines are counted on the `END` line, so a log
shows whether a test wrote its diagnostics. `BYTES` and `PHASE` lines are not counted.

## Rules

- Per-test state lives in the test's own `TestContext.Properties`; tests run in parallel
  at method level, so nothing static is mutable.
- Never wait for real time to test timing: the helper takes a `TimeProvider`, and its
  own tests (`Curl.Core.UnitTests/TestDiagnosticsTests.cs`) use `FakeTimeProvider`.
