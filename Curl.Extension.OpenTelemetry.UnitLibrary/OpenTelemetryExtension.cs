using System.Security.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;
using Curl.Extension.Abstractions.Transfer;

namespace Curl.Extension.OpenTelemetry;

/// <summary>
/// The <c>--otel-endpoint</c> extension (ADR-1134): OpenTelemetry tracing of HTTP transfers. It sends a W3C Trace Context
/// <c>traceparent</c> with each HTTP request and exports the transfer's client span over OTLP/HTTP in JSON.
/// <c>Curl.Extension.Composition</c> registers its option, description and transfer hook.
/// </summary>
public static class OpenTelemetryExtension
{
    /// <summary>Gets the <c>--otel-endpoint</c> option, whose value is the OTLP/HTTP traces endpoint.</summary>
    public static ExtensionOption Option { get; } = new(
        "otel-endpoint",
        ExtensionOptionArgument.Value,
        "Send a W3C traceparent and export each HTTP transfer's span as OTLP/HTTP JSON to <url> (such as http://localhost:4318/v1/traces)");

    /// <summary>Gets what <c>curl --extended-help</c> says about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "otel",
        "Traces HTTP transfers with OpenTelemetry: sends a W3C Trace Context traceparent and exports each transfer's client span over OTLP/HTTP JSON.",
        [],
        [Option],
        new Uri("https://www.w3.org/TR/trace-context/"));

    /// <summary>Gives the extension's transfer hook when the option group gave <c>--otel-endpoint</c>.</summary>
    /// <param name="values">The option group's extension options.</param>
    /// <returns>The hook, or <see langword="null" /> when <c>--otel-endpoint</c> was not given.</returns>
    public static ITransferHook? HookFor(ExtensionOptionValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ValueOf(Option) is { } endpoint ? new OpenTelemetryTransferHook(endpoint, RandomNumberGenerator.Fill) : null;
    }
}
