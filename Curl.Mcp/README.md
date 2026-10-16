# Curl.Mcp

An MCP server that runs curl for AI agents. It serves Curl, the drop-in C# replacement for the
curl command-line tool (<https://github.com/StewartScottRogers/Curl>), over standard input and
output, using the Model Context Protocol (MCP). Any MCP client can use it: GitHub Copilot in VS
Code and Visual Studio, Claude, and others.

It is a thin host. The tool logic lives in `Curl.Mcp.UnitLibrary` (base class library only);
this project holds the Microsoft MCP SDK wiring and the packing. Design: ADR-0548 and
`Documentation/Planning/Curl-Mcp.md`.

## Run it

```
dnx Curl.Mcp
```

The server speaks MCP on standard input and output; its log lines go to standard error. Set
`CURL_MCP_ROOT` first (see Environment), or every file access is refused.

## Tools

| Tool | Does |
| --- | --- |
| `curl` | Runs curl in-process with an argument array (`args`, optional `stdin`), exactly as the command line would; returns standard output, standard error and the exit code. A nonzero exit is a tool error. |
| `transfer` | Makes one request described by fields (`url` required; `method`, `headers`, `body`, `followRedirects`, `timeoutSeconds`, `insecure`, `proxy`, `userAgent`); returns the status, headers, body (text, or base64 when not UTF-8) and transfer information. `file:` URLs are refused. |
| `parse_url` | Parses a URL with libcurl's URL API; returns the rebuilt URL and its parts (scheme, user, password, options, host, port, path, query, fragment, zoneId). |
| `build_url` | Builds a URL from its parts with libcurl's URL API; returns the whole URL. |
| `version` | Reports what `curl -V` reports: the version, protocols, features and TLS backend. |

## Resources

The `--ai-help` Markdown, so an agent can look up any option:

| Resource | Holds |
| --- | --- |
| `curl://ai-help` | The index of options and categories (`curl --ai-help`). |
| `curl://ai-help/all` | The full reference (`curl --ai-help all`). |
| `curl://ai-help/<category>` | One option category (`curl --ai-help <category>`), one resource per category. |

## Environment

| Variable | Required | Meaning |
| --- | --- | --- |
| `CURL_MCP_ROOT` | yes, to touch any file | Every file curl reads or writes must lie inside this directory. An absolute path of an existing folder; the server stops at start-up if it is set to anything else. |
| `CURL_MCP_MAX_RESPONSE_BYTES` | no | The most bytes of output a tool returns; 1048576 when unset. A positive whole number; anything else stops the server at start-up. |

## File access is confined

An agent driving curl can read and write files, so every path a tool would use must resolve
inside `CURL_MCP_ROOT`, with `..` and symbolic links resolved. A relative path resolves against
the root, which is also the server's current directory. The `curl` tool checks the paths in
option values and the paths embedded in them (`-d @file`, `-F name=@file`, `-w @file`,
`file://` URLs, `-K` config files) before curl runs; a path outside the root is refused with a
tool error naming the option, and no transfer starts. An option the server cannot classify is
refused. With `CURL_MCP_ROOT` unset every path is refused, though a transfer that touches no
file still runs. Network access is not restricted.

When a tool's output exceeds `CURL_MCP_MAX_RESPONSE_BYTES` it is cut and the result says so.

## Set it up in an editor

### VS Code

Add to `.vscode/mcp.json` in the workspace (or the user's `mcp.json`):

```json
{
  "servers": {
    "curl": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Curl.Mcp", "--yes"],
      "env": {
        "CURL_MCP_ROOT": "${workspaceFolder}"
      }
    }
  }
}
```

### Visual Studio

Add to `.mcp.json` in the solution folder (or `%USERPROFILE%\.mcp.json` for every solution):

```json
{
  "servers": {
    "curl": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Curl.Mcp", "--yes"],
      "env": {
        "CURL_MCP_ROOT": "C:\\work\\my-project"
      }
    }
  }
}
```

`--yes` lets `dnx` fetch the package without asking.

## Package

`dotnet pack Curl.Mcp` produces a NuGet package of type `McpServer`, with `.mcp/server.json`
naming it `io.github.StewartScottRogers/Curl`. It is self-contained per runtime identifier;
native AOT was tried and is not used (ADR-0598).
