# Curl

<a href="https://github.com/StewartScottRogers/Curl/raw/gource/gource.mp4"><img src="https://github.com/StewartScottRogers/Curl/raw/gource/gource.gif" alt="Gource animation of Curl's commit history across every branch" width="800"></a>

*Curl's history across every branch, drawn by [Gource](https://gource.io) and re-rendered
after new commits and at least once a day. Click it for the full video.*

## What this is

Curl is a port of the open-source [curl](https://curl.se) command-line tool to C# on
.NET 10, built to be a **drop-in replacement**: the same options, the same exit codes,
the same bytes on stdout and stderr, so an existing script cannot tell which binary it
invoked. It publishes as a single native-AOT executable with no runtime to install.

The port is behavioural, not a line-by-line translation of curl's C source. curl's
documentation, its manual page and its test suite are the specification; every protocol
is reimplemented against its RFCs in its own class library, behind interfaces that can be
driven from a unit test without touching a network. The only dependency is the .NET base
class library.

## Built by a dark factory

The port is being written by a *dark factory* - an unattended production line of Claude
Code agents built for this one job, porting curl, and not a general-purpose coding bot.

1. **Plan.** The work is decomposed into small tasks on the [task board](Tasks/README.md),
   one Markdown file per task, each with dependencies and checkable acceptance criteria.
2. **Run.** `RunDarkFactory.cmd` takes the next ready task and hands it to a headless
   Claude Code run that is not allowed to ask a question. It repeats until nothing is
   ready or the shift ends.
3. **Specialise.** Each run uses agents shaped for a curl port, in `.claude/agents`:
   a protocol architect and implementer, a test writer, a build fixer, a code reviewer,
   a coverage auditor, and a conformance auditor that checks options, exit codes and
   output bytes against real upstream curl.
4. **Gate.** Nothing lands unless it builds with warnings as errors and holds every
   library to 100% line and branch coverage, cyclomatic complexity of at most 10 and a
   CRAP score of at most 30.
5. **Escalate.** Anything that needs a human decision is moved to `Blocked` with the
   question written down, and the shift ends with an alarm until someone answers it.

The lights stay off; a person sets direction and answers blocked questions.

## Build and test

Needs the .NET 10 SDK.

```
dotnet build
dotnet test --filter "TestCategory!=Integration"
```

Run a factory shift (Windows):

```
RunDarkFactory.cmd -Hours 4 -MaxTasks 3
```

## Read more

- [Product overview](Documentation/Product/Product-Overview.md) - scope, architecture and roadmap
- [Task board](Tasks/README.md) - what is being worked on, one Markdown file per task

## Licence

See [LICENSE.txt](LICENSE.txt).
