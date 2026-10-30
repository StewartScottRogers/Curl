namespace Curl.Extension.AzureStorage.Bodies;

/// <summary>One block of a Put Block List request body.</summary>
/// <param name="BlockId">The block ID, Base64-encoded as it was sent with Put Block.</param>
/// <param name="Source">Which block list to take the block from.</param>
public sealed record AzureBlockListEntry(string BlockId, AzureBlockListSource Source);
