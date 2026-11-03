using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>
/// Pins <see cref="AzureBlobOperations.BlockSizeFor"/>: a block upload is never committed from more than 50,000 blocks,
/// the Put Block List limit of the Blob service REST API 2025-01-05 (ADR-1213).
/// </summary>
[TestClass]
public sealed class AzureBlobOperationsTests
{
    private const int EightMiB = 8 * 1024 * 1024;

    [TestMethod]
    public void BlockSizeFor_UploadThatNeedsMoreThan50000BlocksOf8MiB_GrowsTheBlockToStayWithin50000()
    {
        long length = (50_000L * EightMiB) + 1;

        int size = AzureBlobOperations.BlockSizeFor(length, EightMiB, AzureBlobOperations.MaxBlockCount);

        Assert.IsGreaterThan(EightMiB, size);
        Assert.IsLessThanOrEqualTo(50_000L, (length + size - 1) / size);
    }

    [TestMethod]
    public void BlockSizeFor_UploadOfExactly50000BlocksOf8MiB_Keeps8MiB()
    {
        Assert.AreEqual(EightMiB, AzureBlobOperations.BlockSizeFor(50_000L * EightMiB, EightMiB, AzureBlobOperations.MaxBlockCount));
    }

    [TestMethod]
    public void BlockSizeFor_UploadPast50000OfTheLargestBlock_FailsWithExit63()
    {
        long length = ((long)Array.MaxLength * 50_000) + 1;

        AzureStorageTransferException failure = Assert.ThrowsExactly<AzureStorageTransferException>(() => AzureBlobOperations.BlockSizeFor(length, EightMiB, AzureBlobOperations.MaxBlockCount));

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, failure.ExitCode);
        Assert.AreEqual($"An Azure Storage upload of {length} bytes needs more than 50000 blocks", failure.Message);
    }
}
