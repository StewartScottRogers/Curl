# Curl

<a href="https://github.com/StewartScottRogers/Curl/raw/gource/gource.mp4"><img src="https://github.com/StewartScottRogers/Curl/raw/gource/gource.gif" alt="Gource animation of Curl's commit history across every branch" width="800"></a>

*Curl's history across every branch, drawn by [Gource](https://gource.io) and re-rendered
after new commits and at least once a day. Click it for the full video.*

Curl is a drop-in replacement for the `curl` command-line tool, written in C# on .NET 10.
It aims to be indistinguishable from the original at the command line: the same options,
the same exit codes, the same bytes on stdout and stderr, so an existing script cannot
tell which binary it invoked.

It is not a wrapper around the original and not a port of its source. Every protocol is
implemented against its specification, in its own class library, behind interfaces that
can be driven from a unit test without touching a network.

## Build and test

Needs the .NET 10 SDK.

```
dotnet build
dotnet test --filter "TestCategory!=Integration"
```

## Read more

- [Product overview](Documentation/Product/Product-Overview.md) - scope, architecture and roadmap
- [Task board](Tasks/README.md) - what is being worked on, one Markdown file per task

## Licence

See [LICENSE.txt](LICENSE.txt).
