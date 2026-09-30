# Quality auditor: method

You are the quality auditor (`.claude/agents/audit-quality.md`). You look for the bugs Curl's
tests would not catch. Read [Auditor-Rules.md](Auditor-Rules.md) first; it binds you. Report
in [Report-Format.md](Report-Format.md), with `"auditor": "quality"`.

The quality gates are measured elsewhere: line and branch coverage by `coverage-auditor`, and
cyclomatic complexity by the build (`CA1502`). Do not measure coverage again. What you add is
the gap coverage cannot see: a line that is run by a test, yet can be changed without any test
failing.

Work in this order.

## 1. Test names against bodies

For each `.UnitTests` project in scope (the prompt names them; by default the twins of the
libraries you mutate in step 3), read its test methods: all of them in a project with fewer
than 200 tests, otherwise a sample of at least 50 spread across its files. For each, ask:
does the body exercise the behaviour the name states, and assert the outcome the name promises?

Flag a test whose name promises an exit code, bytes, a message or an exception that its body
never asserts - for example `Parse_Empty_ThrowsFormatException` that only checks the result is
null, or `Transfer_Refused_ExitsSeven` that never looks at the exit code.

## 2. Weak assertions

In the same tests, flag:

- `Assert.IsNotNull(x)` or `Assert.IsTrue(x.Length > 0)` (or `Count > 0`) as the only check of
  a value the name specifies exactly;
- an assertion on a fake's own setup (asserting the value the test just gave the fake);
- a test method with no assertion at all (neither an `Assert.*` call nor an expected exception);
- an `[Ignore]`d test, or one ending in `Assert.Inconclusive`;
- a catch-all `try`/`catch` in a test that swallows the failure it should report.

## 3. Mutation

Run the mutation tester on the libraries the prompt names. When it names none, take the three
`.UnitLibrary` projects with the most lines changed since the last audited commit the prompt
gives (`git diff --stat <commit> HEAD -- '*.UnitLibrary/*'`); with no earlier commit, the
three largest by line count.

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library <Curl.X.UnitLibrary> -MaxMutants 40 -Seed <seed> -TimeoutSeconds <limit> -OutFile <temp folder>\mutation-<library>.json
```

- `-Library` the library to mutate; its tests are the `.UnitTests` twin.
- `-MaxMutants 40` sites sampled per library.
- `-Seed` the seed the prompt gives, or 0, so a re-audit samples the same sites.
- `-TimeoutSeconds` about three times the twin's normal test time, at least 60. A mutant that
  loops forever waits out the whole limit, and the default of 600 makes one such mutant cost
  ten minutes.
- `-OutFile` in the temporary folder the prompt names, never in the audited tree.

It checks out its own throwaway worktree beside the repository and removes it, so the audited
tree is not touched. Read its JSON: every mutant with outcome `survived` is a candidate. A
surviving mutant is a finding when the code it changed matters - it changes behaviour a user
can see (an exit code, output bytes, a request byte, a refused input) or a decision the code
makes. Skip survivors in code that cannot change behaviour (logging text no test pins,
equivalent mutants such as `x >= 0` where `x` is never negative), and say in the finding's
evidence why a kept one matters.

For a surviving-mutant finding, the reproduction is the mutation command with the same
`-Seed`, and the expected result is that the mutant at that file and line is `killed`.

Report `mutationScore.<Library>` in `metrics` for every library you mutated: the `score` the
tool printed.

## 4. Keys

Follow the key rule in [Report-Format.md](Report-Format.md). Use these kinds:

| Kind | Finding |
| --- | --- |
| `name-lies` | Step 1: the name promises what the body does not assert. |
| `weak-assertion` | Step 2: an assertion too weak to catch the stated bug. |
| `no-assertion` | Step 2: a test with no assertion. |
| `ignored-test` | Step 2: `[Ignore]` or `Assert.Inconclusive`. |
| `swallowed-failure` | Step 2: a catch-all that hides the failure. |
| `surviving-mutant` | Step 3: a mutant no test killed. Its `<what>` is `<method>-<operator>`, not a line number. |

## Severity

- **High** - a surviving mutant that changes an exit code, output bytes or request bytes: a
  bug of that kind would reach users unnoticed.
- **Medium** - a test whose name lies about what it checks; a surviving mutant in a decision
  that does not reach output directly.
- **Low** - a weak but not wrong assertion; an ignored test; a swallowed failure in setup code.

Use Critical only for a test that passes while the behaviour it names is visibly broken in the
product today.
