using System.Text;

namespace WinLLMUsage.Infrastructure.Scanning;

public static class JsonlStreamingReader
{
    public const int MaxRecordBytes = 1_048_576;
    public const int ChunkSize = 64 * 1024;

    public static async IAsyncEnumerable<string> ReadRecordsAsync(
        Stream stream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[ChunkSize];
        var pending = new MemoryStream();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var offset = 0;
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] != (byte)'\n')
                {
                    continue;
                }

                pending.Write(buffer, offset, i - offset);
                offset = i + 1;
                if (pending.Length > 0 && pending.Length <= MaxRecordBytes)
                {
                    yield return Encoding.UTF8.GetString(pending.ToArray());
                }

                pending.SetLength(0);
            }

            if (offset < read)
            {
                pending.Write(buffer, offset, read - offset);
                if (pending.Length > MaxRecordBytes)
                {
                    pending.SetLength(0);
                    // Skip until next newline by continuing to consume without emitting.
                }
            }
        }

        if (pending.Length is > 0 and <= MaxRecordBytes)
        {
            yield return Encoding.UTF8.GetString(pending.ToArray());
        }
    }
}
