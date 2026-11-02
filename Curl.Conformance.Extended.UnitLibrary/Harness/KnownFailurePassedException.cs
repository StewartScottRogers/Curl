namespace Curl.Conformance.Extended.Harness;

/// <summary>Thrown when a case listed in <c>KnownFailures.txt</c> passes, so its line must be removed.</summary>
/// <param name="message">The message naming the requirement ID and its task.</param>
public sealed class KnownFailurePassedException(string message) : Exception(message);
