using System.Text;
using WinLLMUsage.Infrastructure.Scanning;

namespace WinLLMUsage.Infrastructure.Tests;

public sealed class JsonlReaderTests
{
    [Fact]
    public async Task ReadsBoundedRecords()
    {
        var payload = Encoding.UTF8.GetBytes("{\"a\":1}\n{\"b\":2}\n");
        using var stream = new MemoryStream(payload);
        var records = new List<string>();
        await foreach (var record in JsonlStreamingReader.ReadRecordsAsync(stream, CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Equal(2, records.Count);
        Assert.Contains("\"a\":1", records[0]);
    }

    [Fact]
    public async Task SkipsOversizedRecords()
    {
        var huge = new string('x', JsonlStreamingReader.MaxRecordBytes + 10);
        var payload = Encoding.UTF8.GetBytes(huge + "\n{\"ok\":true}\n");
        using var stream = new MemoryStream(payload);
        var records = new List<string>();
        await foreach (var record in JsonlStreamingReader.ReadRecordsAsync(stream, CancellationToken.None))
        {
            records.Add(record);
        }

        Assert.Single(records);
        Assert.Contains("ok", records[0]);
    }
}
