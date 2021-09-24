using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace Xtensible.MangoSeed.Core
{
    public record ExportSettings(bool PrettyPrint);

    public class Exporter
    {
        private static readonly ExportSettings _defaultSettings = new(true);

        private readonly MongoSettings _mongoSettings;

        public Exporter(MongoSettings mongoSettings)
        {
            _mongoSettings = mongoSettings;
        }

        public async Task<Result> ExportAsync(string database, string collectionName, string query, Stream outStream,
            ExportSettings? exportSettings = null, CancellationToken cancellationToken = default)
        {
            exportSettings ??= _defaultSettings;
            var client = MongoClientFactory.Create(_mongoSettings)
                .WithReadPreference(ReadPreference.SecondaryPreferred);

            var db = client.GetDatabase(database);
            var collection = db.GetCollection<BsonDocument>(collectionName);
            if (!BsonDocument.TryParse(query, out BsonDocument parsedQuery))
            {
                return new Result(false, $"{query} could not be parsed");
            }

            var cursor = await collection.FindAsync(parsedQuery, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var newLine = Encoding.UTF8.GetBytes("\n");
            var count = 0;
            var jsonWriterSettings = new JsonWriterSettings {Indent = exportSettings.PrettyPrint, NewLineChars = "\n"};
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new Result(false, "Operation aborted");
                }

                foreach (var doc in cursor.Current)
                {
                    if (count > 0)
                    {
                        await outStream.WriteAsync(newLine, cancellationToken).ConfigureAwait(false);
                    }

                    var json = doc.ToJson(jsonWriterSettings);
                    var buffer = Encoding.UTF8.GetBytes(json);
                    await outStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
                    count++;
                }
            }

            return new Result(true, $"Export complete. Exported {count} document{(count != 1 ? "s" : "")}");
        }
    }
}