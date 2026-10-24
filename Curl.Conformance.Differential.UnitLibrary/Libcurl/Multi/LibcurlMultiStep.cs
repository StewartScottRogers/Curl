namespace Curl.Conformance.Differential.Libcurl.Multi;

/// <summary>One call of a multi or share case, such as <c>["add", 0]</c> or <c>["setopt", 7, "l", 1]</c>.</summary>
/// <param name="Operation">The tester's operation name, such as <c>easy</c>, <c>add</c>, <c>run</c> or <c>sset</c>.</param>
/// <param name="Options">The options an <c>easy</c> step sets on its new easy handle; empty otherwise.</param>
/// <param name="Argument">The step's number (an easy handle's index or milliseconds), or 0 when it has none.</param>
/// <param name="Setting">The option a <c>setopt</c>, <c>sset</c> or <c>ssetnull</c> step sets; <see langword="null"/> otherwise.</param>
public sealed record LibcurlMultiStep(string Operation, IReadOnlyList<LibcurlMultiValue> Options, long Argument, LibcurlMultiValue? Setting);
