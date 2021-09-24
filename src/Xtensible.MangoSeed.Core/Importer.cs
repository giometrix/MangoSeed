using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Xtensible.MangoSeed.Core
{
    public enum ExistingEntryBehavior
    {
        None,
        Truncate,
        Drop,
        Ignore,
        Replace
    }

    public record ImportSettings(int BatchSize, int MaxDegreeOfParallelism,
        ExistingEntryBehavior ExistingEntryBehavior);

    public class Importer
    {
        private static readonly ImportSettings _defaultSettings = new(50, 4, ExistingEntryBehavior.None);
        private readonly MongoSettings _mongoSettings;

        public Importer(MongoSettings mongoSettings)
        {
            _mongoSettings = mongoSettings;
        }

        public async Task<Result> ImportAsync(string database, IEnumerable<string> files,
            ImportSettings? importSettings = null, Action<Result>? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            importSettings ??= _defaultSettings;
            progressReporter ??= result => { };
            int fileCount = 0, recordCount = 0;
            var recordCountLock = new object();

            await files.ParallelForEachAsync(async file =>
            {
                var numRecordsInFile = await ProcessFileAsync(file, database, importSettings, progressReporter).ConfigureAwait(false);
                lock (recordCountLock)
                {
                    fileCount++;
                    recordCount += numRecordsInFile;
                }
            }, importSettings.MaxDegreeOfParallelism, cancellationToken).ConfigureAwait(false);

            return new Result(true,
                $"Import complete. Imported {recordCount} record{(recordCount != 1 ? "s" : "")} from {fileCount} file{(fileCount != 1 ? "s" : "")}");
        }

        private async Task<int> ProcessFileAsync(string file, string database, ImportSettings importSettings,
            Action<Result> progressReporter)
        {
            var recordCount = 0;
            progressReporter(new Result(true, $"Processing {file}..."));
            var collectionName = Path.GetFileNameWithoutExtension(file);
            var client = MongoClientFactory.Create(_mongoSettings);
            var db = client.GetDatabase(database);
            var collection = db.GetCollection<BsonDocument>(collectionName);
            var (batchSize, _, existingEntryBehavior) = importSettings;
            await TruncateDataAsync(db, collection, existingEntryBehavior).ConfigureAwait(false);
            using var sr = new StreamReader(file);
            var batch = new List<BsonDocument>(batchSize);
            await foreach (var d in GetRecordsAsync(sr, file, progressReporter))
            {
                recordCount++;
                if (batch.Count < batchSize)
                {
                    batch.Add(d);
                }
                else
                {
                    await ProcessBatchAsync(collection, batch, existingEntryBehavior).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(collection, batch, existingEntryBehavior).ConfigureAwait(false);
                batch.Clear();
            }

            return recordCount;
        }

        private async Task ProcessBatchAsync(IMongoCollection<BsonDocument> collection, List<BsonDocument> batch,
            ExistingEntryBehavior existingEntryBehavior)
        {
            var filtered = await FilterBatchAsync(collection, batch, existingEntryBehavior).ConfigureAwait(false);
            await DeleteDocumentsToBeReplacedAsync(collection, batch, existingEntryBehavior).ConfigureAwait(false);
            if (filtered.Any())
            {
                await collection.InsertManyAsync(filtered);
            }
        }

        private async Task DeleteDocumentsToBeReplacedAsync(IMongoCollection<BsonDocument> collection,
            List<BsonDocument> batch,
            ExistingEntryBehavior existingEntryBehavior)
        {
            if (existingEntryBehavior == ExistingEntryBehavior.Replace)
            {
                await collection.DeleteManyAsync(CreateInFilter(batch)).ConfigureAwait(false);
            }
        }

        private async Task<IEnumerable<BsonDocument>> FilterBatchAsync(IMongoCollection<BsonDocument> collection,
            IEnumerable<BsonDocument> batch, ExistingEntryBehavior existingEntryBehavior)
        {
            if (existingEntryBehavior == ExistingEntryBehavior.Ignore)
            {
                var existingRecords = await GetExistingRecordsAsync(collection, batch);
                return batch.Where(d => !existingRecords.Contains(d["_id"].AsObjectId));
            }

            return batch;
        }

        private FilterDefinition<BsonDocument> CreateInFilter(IEnumerable<BsonDocument> batch)
        {
            var ids = batch.Select(d => d["_id"].AsObjectId);
            return Builders<BsonDocument>.Filter.In("_id", ids);
        }

        private async Task<IEnumerable<ObjectId>> GetExistingRecordsAsync(IMongoCollection<BsonDocument> collection,
            IEnumerable<BsonDocument> batch)
        {
            var found = await collection.Find(CreateInFilter(batch))
                .Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync().ConfigureAwait(false);
            return found.Select(d => d["_id"].AsObjectId);
        }

        private async Task TruncateDataAsync(IMongoDatabase db, IMongoCollection<BsonDocument> collection,
            ExistingEntryBehavior existingEntryBehavior)
        {
            switch (existingEntryBehavior)
            {
                case ExistingEntryBehavior.Truncate:
                    await collection.DeleteManyAsync(Builders<BsonDocument>.Filter.Empty).ConfigureAwait(false);
                    break;
                case ExistingEntryBehavior.Drop:
                    await db.DropCollectionAsync(collection.CollectionNamespace.CollectionName).ConfigureAwait(false);
                    break;
            }
        }

        private async IAsyncEnumerable<BsonDocument> GetRecordsAsync(StreamReader stream, string source,
            Action<Result> progressReporter)
        {
            var sb = new StringBuilder();
            var line = await stream.ReadLineAsync().ConfigureAwait(false);
            var parseSuccessResult = new Result(true, String.Empty);
            var lineNumber = 0;
            while (line != null)
            {
                lineNumber++;
                if (line.StartsWith("/"))
                {
                    continue;
                }

                if (line.StartsWith("{") && sb.Length > 0)
                {
                    var (doc, result) = getCurrentDocument();
                    if (result.IsSuccess)
                    {
                        yield return doc;
                    }
                    else
                    {
                        progressReporter(result);
                    }
                }

                sb.Append(line);
                line = await stream.ReadLineAsync().ConfigureAwait(false);
            }

            if (sb.Length > 0)
            {
                var (doc, result) = getCurrentDocument();
                if (result.IsSuccess)
                {
                    yield return doc;
                }
                else
                {
                    progressReporter(result);
                }
            }

            (BsonDocument doc, Result result) getCurrentDocument()
            {
                var record = sb.ToString();
                sb.Clear();
                if (!BsonDocument.TryParse(record, out BsonDocument doc))
                {
                    return (doc, new Result(false, $"Error parsing one or near line {lineNumber} in {source}"));
                }

                return (doc, parseSuccessResult);
            }
        }
    }
}