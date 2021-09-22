﻿using System;
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
                var numRecordsInFile = await ProcessFileAsync(file, database, importSettings, progressReporter);
                lock (recordCountLock)
                {
                    fileCount++;
                    recordCount += numRecordsInFile;
                }
            }, importSettings.MaxDegreeOfParallelism, cancellationToken);

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
            await TruncateDataAsync(db, collection, existingEntryBehavior);
            using var sr = new StreamReader(file);
            var batch = new List<BsonDocument>(batchSize);
            await foreach (var d in GetRecordsAsync(sr))
            {
                recordCount++;
                if (batch.Count < batchSize)
                {
                    batch.Add(d);
                }
                else
                {
                    await ProcessBatchAsync(collection, batch, existingEntryBehavior);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(collection, batch, existingEntryBehavior);
                batch.Clear();
            }

            return recordCount;
        }

        private async Task ProcessBatchAsync(IMongoCollection<BsonDocument> collection, List<BsonDocument> batch,
            ExistingEntryBehavior existingEntryBehavior)
        {
            var filtered = await FilterBatchAsync(collection, batch, existingEntryBehavior);
            await DeleteDocumentsToBeReplacedAsync(collection, batch, existingEntryBehavior);
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
                await collection.DeleteManyAsync(CreateInFilter(batch));
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
                .Project(Builders<BsonDocument>.Projection.Include("_id")).ToListAsync();
            return found.Select(d => d["_id"].AsObjectId);
        }

        private async Task TruncateDataAsync(IMongoDatabase db, IMongoCollection<BsonDocument> collection,
            ExistingEntryBehavior existingEntryBehavior)
        {
            switch (existingEntryBehavior)
            {
                case ExistingEntryBehavior.Truncate:
                    await collection.DeleteManyAsync(Builders<BsonDocument>.Filter.Empty);
                    break;
                case ExistingEntryBehavior.Drop:
                    await db.DropCollectionAsync(collection.CollectionNamespace.CollectionName);
                    break;
            }
        }

        private async IAsyncEnumerable<BsonDocument> GetRecordsAsync(StreamReader stream)
        {
            var sb = new StringBuilder();
            var line = await stream.ReadLineAsync();
            while (line != null)
            {
                if (line.StartsWith("/"))
                {
                    continue;
                }

                if (line == "{" && sb.Length > 0)
                {
                    var record = sb.ToString();
                    yield return BsonDocument.Parse(record);
                    sb.Clear();
                }

                sb.Append(line);
                line = await stream.ReadLineAsync();
            }

            if (sb.Length > 0)
            {
                yield return BsonDocument.Parse(sb.ToString());
            }
        }
    }
}