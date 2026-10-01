namespace Curl.Networking;

/// <summary>The outcome of <see cref="ServiceBindingRecordDecoder.Decode" />.</summary>
/// <param name="Failure"><see cref="ServiceBindingFailure.None" /> when the record decoded; otherwise why not, and then <paramref name="Record" /> is <see langword="null" />.</param>
/// <param name="Record">The decoded record, or <see langword="null" /> on a failure.</param>
public sealed record ServiceBindingDecoding(ServiceBindingFailure Failure, ServiceBindingRecord? Record);
