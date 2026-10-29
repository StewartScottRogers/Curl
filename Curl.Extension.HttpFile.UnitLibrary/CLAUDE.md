# Curl.Extension.HttpFile.UnitLibrary

The .http request file extension (ADR-0999, ADR-1073), tested against the Visual Studio .http file format
(learn.microsoft.com/aspnet/core/test/http-files) and the VS Code REST Client format it shares. Namespace
`Curl.Extension.HttpFile`. It references only `Curl.Extension.Abstractions` (and through it
`Curl.Protocol.Abstractions`) and the base class library; `http-client.env.json` is read with
`System.Text.Json`'s `JsonDocument` (no serializer, so AOT-safe).

- `HttpFileOptions` declares `--http-file` and `--http-env` and the extension's `Description`;
  `Curl.Extension.Composition`'s option and description catalogues list them.
- `HttpFileArgumentExpander` is an argument expander (ADR-1040, ADR-1051) the expander catalogue lists: it replaces
  `--http-file <file>` and a `--http-env <name>` in its `--next` group with the curl arguments of every request in
  the file, joined by `--next`, before Curl.Console parses the command line. It never touches a transfer.
- `HttpFileParser` reads the requests (`###` separators, comments, `@name = value`, request line, query lines,
  headers, body or `< file`); `HttpFileVariables` substitutes `{{name}}` and `{{$processEnv NAME}}`;
  `HttpFileEnvironment` reads `$shared` and the named environment; `HttpFileRequest` gives one request's arguments.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.HttpFile.UnitTests`.
