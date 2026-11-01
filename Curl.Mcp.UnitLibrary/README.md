# Curl.Mcp.UnitLibrary

All of the MCP server's tool logic (ADR-0548), in a library that uses the base class library
only and knows nothing of the MCP SDK: path confinement, the response cap, result and error
plumbing, and the logic of each tool. [`Curl.Mcp`](../Curl.Mcp) is the thin host that wires
these types to the Model Context Protocol; every behaviour worth testing lives here.

## How it is used

`Curl.Mcp` builds the library's tools once at start-up and each `[McpServerTool]` method
forwards to one of them. A tool takes its collaborators by injection (`ICurlCommandRunner` for
the `curl` tool, `ICurlEngine` for `transfer` and `version`, an `ICurlUrlHandle` factory for
`parse_url` and `build_url`), so a test replaces them without a network. Confinement is
checked through `McpConfinementRoot`:

```csharp
using Curl.Mcp;

McpConfinementRoot root = McpConfinementRoot.FromEnvironment(Environment.GetEnvironmentVariable);
McpPathCheck check = root.Check("downloads/page.html");
// check.IsAllowed is true only when the path, with ".." and symbolic links resolved,
// lies inside CURL_MCP_ROOT; check.Reason says why not.
```

## Type map

Namespace `Curl.Mcp`. The library has no subfolders.

| Concern | Types |
| --- | --- |
| Configuration | `McpConfinementRoot` (`CURL_MCP_ROOT`; `Check` answers with an `McpPathCheck` holding an `McpPathVerdict`: `Inside`, `Outside`, `Unresolvable` or `NoRootConfigured`), `McpResponseCap` (`CURL_MCP_MAX_RESPONSE_BYTES`, 1048576 by default), `McpConfigurationException` (a bad variable value). |
| Option path tables | `McpOptionPathTable` says how every curl option uses a local path, as an `McpOptionPathUse` (`None`, `ReadsFile`, `WritesFile`, `WritesDirectory`, `ReadsOrWritesFile`, `ReadsFileOrLiteral`, `ValueEmbedded`, `UnixSocketPath`); an option it does not name is refused. `McpOptionValuePaths`, `McpValuePath` and `McpConfigFileSyntax` find the paths embedded in values (`-d @file`, `-F name=@file`, `-w @file`) and in `-K` config files. |
| Argument checking | `McpArgumentChecker` checks a whole curl argument array, including embedded paths, `file:` URLs and `-K` files, and answers with an `McpArgumentCheck`. |
| Results and errors | `McpToolFailure` (a tool error, of an `McpToolFailureKind`: `CurlExit`, `PathRefused`, `Cancelled`, `InvalidRequest`, `UrlRefused`), `McpToolResult`, `McpToolRunner` (passes the request's cancellation token through), `McpReturnedBytes` (text, or base64 when not UTF-8, cut to the cap), `McpCappedCaptureStream`. |
| `curl` tool | `McpCurlTool`, `ICurlCommandRunner`, `McpCurlRunOutput`, `McpCurlToolResult`. |
| `transfer` tool | `McpTransferTool`, `McpTransferRequest` (with `McpTransferHeader` and `McpTransferBody`), `McpTransferRequestCheck`, `McpTransferOptions` (request to `EasyOptions`), `McpResponseHeaderCollector`, `McpTransferInfo`, `McpTransferToolResult`. |
| `version` tool | `McpVersionTool`, `McpVersionToolResult`. |
| `parse_url`, `build_url` tools | `McpUrlTool`, `McpUrlParts`, `McpParseUrlToolResult`, `McpBuildUrlToolResult`. |
| `--ai-help` resources | `McpAiHelpResourceCatalogue` lists one `McpAiHelpResource` per subject (the index, `all`, and each category of `CurlHelpText.Categories`) under `curl://ai-help`; `ResourcesWithExtensions` adds `curl://ai-help/extensions`, whose Markdown the host supplies. |

## Quality gates and sibling projects

- References `Curl.Api.UnitLibrary` (the engine seam and result codes) and
  `Curl.Cli.UnitLibrary` (the help text); never the MCP SDK, which only `Curl.Mcp` may use.
- Held to the root `CLAUDE.md` gates: 100% line and branch coverage, cyclomatic complexity
  at most 10 per method, CRAP score at most 30.
- Its tests are in `Curl.Mcp.UnitTests`, which tests this library and `Curl.Mcp`; the
  project grants that assembly `InternalsVisibleTo`.
- See [CLAUDE.md](CLAUDE.md) for the rules for changing it, and the
  [Curl.Mcp README](../Curl.Mcp/README.md) for the tools as an agent sees them.
