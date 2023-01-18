namespace Dox.Extensions;

public static class DropboxClientExtensions
{
    public const int ChunkedThreshold = 150 * 1024 * 1024;
    public const int MinimumChuckSizeInKilobytes = 128;
    public const int MinimumChunkSize = MinimumChuckSizeInKilobytes * 1024;
    public const int DefaultChunkSizeInKilobytes = 1024;
    public const int DefaultTimeoutInSeconds = 100;

    public static string CombinePath(string folder, string filename) => folder == "/" ? $"{folder}/" : $"{folder}/{filename}";

    public static async Task<FileMetadata?> Upload(this DropboxClient client, string folder, string filename, Stream stream)
    {
        var destinationPath = CombinePath(folder, filename);

        return await client.Files.UploadAsync(destinationPath, WriteMode.Overwrite.Instance, body: stream);
    }

    public static async Task<FileMetadata?> UploadChunked(this DropboxClient client, string folder, string filename,
        Stream stream, int chunkSize, IProgress<long> progress, CancellationToken cancellationToken)
    {
        var chunks = (int)Math.Ceiling((double)stream.Length / chunkSize);

        var buffer = new byte[chunkSize];
        string? sessionId = null;
        FileMetadata? fileMetadata = null;

        var destinationPath = CombinePath(folder, filename);

        for (var i = 0; i < chunks; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            var byteRead = await stream.ReadAsync(buffer, 0, chunkSize, cancellationToken);

            using MemoryStream memoryStream = new(buffer, 0, byteRead);

            if (i == 0)
            {
                var result = await client.Files.UploadSessionStartAsync(body: memoryStream);

                sessionId = result.SessionId;
            }
            else
            {
                UploadSessionCursor cursor = new(sessionId, (ulong)(chunkSize * i));

                if (i == chunks - 1)
                {
                    fileMetadata = await client.Files.UploadSessionFinishAsync(cursor,
                        new CommitInfo(destinationPath, WriteMode.Overwrite.Instance), body: memoryStream);

                    if (!cancellationToken.IsCancellationRequested)
                        progress.Report(stream.Length);
                }
                else
                {
                    await client.Files.UploadSessionAppendV2Async(cursor, body: memoryStream);

                    if (!cancellationToken.IsCancellationRequested)
                        progress.Report(i * chunkSize);
                }
            }
        }

        return fileMetadata;
    }
}