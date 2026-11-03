# Quality auditor: method

You are the quality auditor (`.claude/agents/audit-quality.md`). You look for the bugs Curl's
tests would not catch. Read [Auditor-Rules.md](Auditor-Rules.md) first; it binds you. Report
in [Report-Format.md](Report-Format.md), with `"auditor": "quality"`.

The quality gates are measured elsewhere: line and branch coverage by `coverage-auditor`, and
cyclomatic complexity by the build (`CA1502`). Do not measure coverage again. What you add is
the gap coverage cannot see: a line that is run by a test, yet can be changed without any test
failing.

Work in this order.

## 0. Scan every test project

Run the scan over the whole audited tree, every `*.UnitTests` project included, before you
choose any project to read (BL-1368):

```powershell
powershell -NoProfile -File Audit/Tools/Find-WeakTests.ps1 -Root <audited tree> -OutFile <temp folder>\weak-tests.json
```

It lists candidates of six kinds: `ignored-test` (`[Ignore]` or `Assert.Inconclusive`),
`no-assertion` (no assertion once comments are removed), `weak-assertion`, `unasserted-fake-value`,
`commented-assertion` and `name-lies` (a name with `ExitsWith<N>` or `ThrowsExit<N>` whose body never mentions N or its
`CurlExitCode`, or `_Throws<X>` whose body never mentions X). `weak-assertion` is judged per
assertion, not only per test: a test whose every assertion is `IsNotNull` or an `IsTrue` of a
length, count or `Any()` above zero; a weak assertion that is the only check of its variable
while the test's other assertions check other values (an exact `AreEqual` on the bytes weakened
to `IsNotNull` beside a kept exit-code check); and an `IsNotNull` of a value that can never be
null (a `ToString`, `GetString`, `Substring` or `string.Join` result, an interpolated string, a
`new` expression, a `.Length` or `.Count`). `commented-assertion` is an `Assert.`,
`CollectionAssert.` or `StringAssert.` call commented out inside the test, whatever assertions
remain: the outcome it checked is no longer checked by it. `unasserted-fake-value` is the sharp
form of a weak check that is the only check of its value: the test's fakes return an exact string
(a lambda's or a switch arm's result, a `return "..."`) and no assertion mentions it, not even
inside a longer expected value - the test arranges the outcome and never looks at it. Read every
candidate's test: file the ones that really are what the scan says, under steps 1 and 2's kinds,
and say in your summary how many candidates you read and how many you filed. A candidate is not a
finding until you have read it; for a `commented-assertion`, check whether any other assertion
still checks that outcome, and file it when none does.

### Triage: a reason for every candidate you do not file

A scan with hundreds of candidates invites dismissing them as a class ("mostly false positives").
Never do that; a class is not a reason. Write one line per candidate to
`<temp folder>\weak-tests-triage.tsv` - file, test, kind, `filed` or `dismissed`, reason, and the
quoted evidence - and dismiss only for one of these reasons, quoting what proves it:

| Reason | Quote |
| --- | --- |
| `checked-exactly` | the assertion (or the assert helper's line) in this test that checks the value exactly |
| `name-promises-only-existence` | the words of the name that promise only that something exists, is stored or does not throw (`_IsStoredWithoutRefusal`, `_DoesNotThrow`), never its content |
| `scanner-misread` | what the scanner misread: a brace in a string, a helper it did not see assert, a joiner word after `Throws` |
| `value-cannot-matter` | why no value of it could be wrong: an opaque handle, an instance only compared by reference elsewhere |

These are mechanical, so apply them as written:

- An `unasserted-fake-value` candidate is filed (`weak-assertion`, Medium) unless you quote an
  exact check of the fake's value (`checked-exactly`). A name that reads, returns, uses, takes,
  writes, sends or gives something promises its content, so `name-promises-only-existence` does
  not dismiss it.
- A `weak-assertion` that is the only check of its value is filed when the test's name says what
  that value holds or where it comes from (`_ReadsTheHomeGatewayFile`, `_ReturnsTheBody`,
  `_UsesTheProxy`): `IsNotNull` cannot tell the right value from a wrong one.
- In your summary's method table, give the dismissals counted by reason. The number of candidates you report as read must equal the triage file's line count.

## 1. Test names against bodies
Beyond step 0's candidates, for each `.UnitTests` project in scope (the prompt names them; by default the twins of the
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
three largest by line count. Then, every audit, add three more drawn from every library with a
`.UnitTests` twin, so that over audits every library is mutated, not only those the factory
changed most:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -DrawLibraries 3 -Seed <seed> -Except <the three most changed, comma-separated>
```

It prints the three names as JSON, drawn with the seed the prompt gives (a new one each audit).
Mutate the drawn three with `-MaxMutants 25`; name all six in your summary, saying which were
drawn.

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

For a surviving-mutant finding, the reproduction targets that one mutant, never the sample:

- `reproduction.mutation` is `<file>:<line>:<operator>`, copied from the tool's JSON for the
  mutant (`file`, `line`, `operator`), e.g.
  `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:77:true`. Always write it: it is what
  lets the audit run rerun the mutant itself and close the finding on that evidence (ADR-0422).
- `reproduction.command` is
  `powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site <file>:<line>:<operator> -Member <member> -ExcludeBaselineFailures -TimeoutSeconds <limit>`,
  with `<member>` the mutant's `member` from the JSON.
- The expected result is that the mutant is `killed`; the actual, that it `survived`.

The findings writer replaces the key of a finding with a `reproduction.mutation` by one built
from the site: `quality:<file>:<member>-<operator word>:surviving-mutant`. Write the key as
below anyway; it is used when the site is missing.

A library whose unmutated tests already fail: run it with `-ExcludeBaselineFailures`, which
leaves out the failing tests and names them in `excludedTests`, rather than skipping the
library. Report the failing tests as a finding of their own if they are not one already.

### Re-auditing a surviving-mutant finding

Run the finding's reproduction command - the targeted `-Site` run - on the tree you audit,
never a fresh sample. A sample that did not include the site says nothing about it. Report:

- `"reproduces": true` when the mutant `survived`;
- `"reproduces": false` when it was `killed` or `timedOut`;
- `"reproduces": null` when you could not tell - the outcome was `site-missing` or
  `stillborn`, the baseline stopped the run, or the finding's reproduction is the old sampled
  command and its site was not in your sample. `null` is recorded as "not re-audited"; never
  report a site you did not run as `false`.

Report `mutationScore.<Library>` in `metrics` for every library you mutated: the `score` the
tool printed.

## 4. Keys

Follow the key rule in [Report-Format.md](Report-Format.md). Use these kinds:

| Kind | Finding |
| --- | --- |
| `name-lies` | Step 1: the name promises what the body does not assert. |
| `weak-assertion` | Step 2 (and step 0's `weak-assertion` and `unasserted-fake-value` candidates): an assertion too weak to catch the stated bug. |
| `commented-assertion` | Step 0: an assertion commented out, with no other assertion checking its outcome. |
| `no-assertion` | Step 2: a test with no assertion. |
| `ignored-test` | Step 2: `[Ignore]` or `Assert.Inconclusive`. |
| `swallowed-failure` | Step 2: a catch-all that hides the failure. |
| `surviving-mutant` | Step 3: a mutant no test killed. Its `<what>` is `<member>-<operator word>` (the tool's `member`; `eq`, `ne`, `lt`, `gt`, `le`, `ge`, `and`, `or`, `plus1`, `minus1`, `true`, `false`, `not`), not a line number. |

## Severity

- **High** - a surviving mutant that changes an exit code, output bytes or request bytes: a
  bug of that kind would reach users unnoticed.
- **Medium** - a test whose name lies about what it checks; an `unasserted-fake-value` test,
  whose fake's exact value nothing checks; a commented-out assertion that
  leaves its outcome unchecked; a surviving mutant in a decision that does not reach output
  directly.
- **Low** - a weak but not wrong assertion; an ignored test; a swallowed failure in setup code.

Use Critical only for a test that passes while the behaviour it names is visibly broken in the
product today.

## Method counts

Run every step above on every audit; re-audits come on top, never instead. Report `method.librariesMutated` and `method.testsRead` (step 0's candidates read plus the tests read in step 1) in `metrics` ([Report-Format.md](Report-Format.md#method-counts)): a report without them marks you unreliable (BL-1364).
