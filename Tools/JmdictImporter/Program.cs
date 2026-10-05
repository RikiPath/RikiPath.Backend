using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using Npgsql;

const string defaultUrl = "ftp://ftp.edrdg.org/pub/Nihongo/JMdict_e.gz";
const int batchSize = 500;

var connectionString = GetOption("--connection");
var url = GetOption("--url") ?? defaultUrl;

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Usage: dotnet run --project .\\Tools\\JmdictImporter -- --connection \"Host=...;Database=...;Username=...;Password=...\"");
    return 2;
}

Console.WriteLine("Downloading JMdict...");
using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
await using var download = await OpenDownloadStreamAsync(url, httpClient);
await using var gzip = new GZipStream(download, CompressionMode.Decompress);
using var reader = XmlReader.Create(gzip, new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Ignore,
    IgnoreComments = true,
    IgnoreWhitespace = true,
    Async = true
});

await using var connection = await NpgsqlDataSource.Create(connectionString).OpenConnectionAsync();
await using var transaction = await connection.BeginTransactionAsync();

try
{
    await using (var delete = new NpgsqlCommand(
        "DELETE FROM \"JapaneseDictionaryEntries\" WHERE \"Source\" = 'JMdict';",
        connection, transaction))
    {
        await delete.ExecuteNonQueryAsync();
    }

    var batch = new List<DictionaryRow>(batchSize);
    var imported = 0;

    while (await reader.ReadAsync())
    {
        if (reader.NodeType != XmlNodeType.Element || reader.Name != "entry")
            continue;

        var entry = await ReadEntryAsync(reader);
        foreach (var row in entry)
        {
            batch.Add(row);
            if (batch.Count < batchSize)
                continue;

            await InsertBatchAsync(connection, transaction, batch);
            imported += batch.Count;
            batch.Clear();
            Console.Write($"\rImported {imported:N0} entries...");
        }
    }

    if (batch.Count > 0)
    {
        await InsertBatchAsync(connection, transaction, batch);
        imported += batch.Count;
    }

    await transaction.CommitAsync();
    Console.WriteLine($"\rImported {imported:N0} JMdict entries successfully.");
    return 0;
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

string? GetOption(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static async Task<List<DictionaryRow>> ReadEntryAsync(XmlReader reader)
{
    var document = await XmlDocumentAsync(reader);
    var spellings = document
        .SelectNodes("/entry/k_ele/keb")?
        .Cast<XmlNode>()
        .Select(x => x.InnerText.Trim())
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToList() ?? [];
    var readings = document
        .SelectNodes("/entry/r_ele/reb")?
        .Cast<XmlNode>()
        .Select(x => x.InnerText.Trim())
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToList() ?? [];
    var meanings = document
        .SelectNodes("/entry/sense/gloss")?
        .Cast<XmlNode>()
        .Select(x => x.InnerText.Trim())
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToList() ?? [];

    var meaning = string.Join("; ", meanings);
    if (readings.Count == 0 || meaning.Length == 0)
        return [];

    var surfaces = spellings.Count > 0 ? spellings : readings;
    return (
        from reading in readings
        from surface in surfaces
        select new DictionaryRow(
            surface,
            reading,
            meaning,
            surface.Any(c => c is >= '\u30a1' and <= '\u30f6'))
    ).Distinct().ToList();
}

static async Task<XmlDocument> XmlDocumentAsync(XmlReader reader)
{
    var document = new XmlDocument();
    using var nodeReader = reader.ReadSubtree();
    document.Load(nodeReader);
    return document;
}

static async Task<Stream> OpenDownloadStreamAsync(string url, HttpClient httpClient)
{
    if (!url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
        return await httpClient.GetStreamAsync(url);

    var request = WebRequest.Create(url);
    var response = await request.GetResponseAsync();
    return response.GetResponseStream();
}

static async Task InsertBatchAsync(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    IReadOnlyList<DictionaryRow> rows)
{
    var sql = new StringBuilder("""
        INSERT INTO "JapaneseDictionaryEntries"
            ("Surface", "ReadingKana", "ReadingRomaji", "Meaning",
             "IsKatakana", "Source", "IsDeleted", "CreatedDate")
        VALUES
        """);
    await using var command = new NpgsqlCommand { Connection = connection, Transaction = transaction };

    for (var i = 0; i < rows.Count; i++)
    {
        if (i > 0)
            sql.Append(", ");

        sql.Append($"(@surface{i}, @reading{i}, '', @meaning{i}, @katakana{i}, 'JMdict', false, now())");
        command.Parameters.AddWithValue($"surface{i}", rows[i].Surface);
        command.Parameters.AddWithValue($"reading{i}", rows[i].Reading);
        command.Parameters.AddWithValue($"meaning{i}", rows[i].Meaning);
        command.Parameters.AddWithValue($"katakana{i}", rows[i].IsKatakana);
    }

    command.CommandText = sql.ToString();
    await command.ExecuteNonQueryAsync();
}

record DictionaryRow(string Surface, string Reading, string Meaning, bool IsKatakana);
