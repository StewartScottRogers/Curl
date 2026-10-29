# ADR-1073 — --http-file expands a .http file into curl arguments, with --http-env choosing the environment

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4154.

## Context

BL-4154 adds the extension `Curl.Extension.HttpFile.UnitLibrary`: run the requests of a Visual Studio / VS Code
REST Client `.http` file. Upstream curl has no such option, so there is no curl output to match; the format is
specified by Microsoft's "Use .http files in Visual Studio" (learn.microsoft.com/aspnet/core/test/http-files, as
published for Visual Studio 2022 17.8 and later) and the VS Code REST Client README (Huachao Mao, 0.25), which share
the syntax used here. A .http file supplies whole requests, so the extension is an argument expander (ADR-1040,
ADR-1051), not a transfer hook: curl's own option parser and transfers do the rest.

## Decision

- **Option names.** `--http-file <file>` names the file. `--http-env <name>`, a companion option in the same
  `--next` group, chooses the environment. Neither name is a curl option.
- **Expansion.** Each request becomes one `--next` group: `--head` for HEAD; `-X <method>` unless curl already infers
  it (GET with no body, POST with one); the URL; `-H` per header (an empty value as `Name;`, which curl sends
  empty); an inline body as `--data-raw` (so a leading `@` is literal) and a `< path` body as
  `--data-binary @path`. Options written before `--http-file` stay in the first group only, as curl's `--next`
  works.
- **Syntax read.** Requests separated by lines starting with `###`; blank lines, `#` and `//` comments and
  `@name = value` file variables before a request line; the request line `[METHOD] URL [HTTP/version]` (the version
  is ignored; a first word that is not an upper-case method makes the line a GET of that URL); `?` and `&` query
  lines right after it; headers up to the first blank line; the body to the next separator, its trailing blank lines
  dropped, joined with the file's own line ending (CRLF when the file has one, else LF). A UTF-8 byte order mark is
  dropped.
- **Variables.** `{{name}}` and `{{$processEnv NAME}}` (an unset variable gives the empty string). File variables are
  substituted where they are defined, in file order, and replace an environment variable of the same name.
- **Environments.** `--http-env <name>` reads `http-client.env.json` in the .http file's directory (the path keeps
  the directory as written): a JSON object of environments, each an object of variables, read with `JsonDocument`.
  `$shared` is set first and the named environment replaces its values; a non-string value is its JSON text.
- **Failures** end the run before any transfer: the .http file or environment file unreadable, exit 26
  (`READ_ERROR`, as `-K` does); no request in the file, an undefined variable, a header line without a colon, an
  environment file that is not a JSON object of objects, or an environment it lacks, exit 2 (`FAILED_INIT`). Under
  `CURL_EXTENSIONS=0` neither option exists, so curl refuses them as unknown (exit 2).

## Left out, and why

- **Dynamic system variables** (`{{$guid}}`, `{{$randomInt}}`, `{{$timestamp}}`, `{{$datetime}}`,
  `{{$localDatetime}}`, `{{$dotenv}}`, `{{$aadToken}}`) and **request variables** (`# @name` plus
  `{{name.response.body.$.x}}`): they need either values a script cannot repeat or the response of an earlier
  request, which an expansion before parsing cannot see. An unknown one fails with exit 2 rather than send a literal
  `{{...}}`.
- **`http-client.env.json.user`, `.vscode/settings.json` environments and secret providers** (Azure Key Vault,
  DPAPI): one file beside the .http file keeps the lookup predictable; secrets come in through `$processEnv`.
- **`# @prompt`, `# @no-redirect`, `# @no-cookie-jar` and other request settings**: they are read as comments; curl's
  own options (`-L`, `-b`) already say what they would.
- **Content-Type**: a body without a `Content-Type` header gets curl's default
  (`application/x-www-form-urlencoded`), as `--data-raw` always does; write the header to change it.

## Consequences

- `curl --http-file api.http --http-env dev` runs every request of a checked-in .http file with curl's own options,
  output and exit codes, and `-v`, `-o` and the rest apply to the first request as they would after `--next`.
- Adding request variables later needs a hook that runs after a transfer, not this expander.
