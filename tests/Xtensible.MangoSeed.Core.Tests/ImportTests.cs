using System;
using System.Linq;
using System.Threading.Tasks;
using Dasync.Collections;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Xtensible.MangoSeed.Core.Tests
{
    public class ImportTests : IDisposable
    {
        public ImportTests()
        {
            Mongo = MongoDbRunner.Start();
        }

        public void Dispose()
        {
            Mongo.Dispose();
        }

        private MongoDbRunner Mongo { get; }
        private string MongoServer => Mongo.ConnectionString.Replace("mongodb://", "").Replace("/", "");

        private void Seed()
        {
            Mongo.Import("test", "animals", "./data/animals.json", true);
        }

        [Fact]
        public async Task import_existing_entry_behavior_of_drop()
        {
            Seed();
            var importer = new Importer(new MongoSettings(MongoServer));
            var result = await importer.ImportAsync("test", new[] { "./data/exported/animals.json" },
                new ImportSettings(5, 5, ExistingEntryBehavior.Drop));
            Assert.True(result.IsSuccess);
            Assert.Equal("Import complete. Imported 3 records from 1 file", result.Message);
        }

        [Fact]
        public async Task import_existing_entry_behavior_of_ignore()
        {
            Seed();
            var mongoDb = new MongoClient(Mongo.ConnectionString);
            var collection = mongoDb.GetDatabase("test").GetCollection<BsonDocument>("animals");
            await collection.UpdateManyAsync("{}", BsonDocument.Parse("{$set:{name:'x'}}"));
            var importer = new Importer(new MongoSettings(MongoServer));
            var result = await importer.ImportAsync("test", new[] { "./data/exported/animals.json" },
                new ImportSettings(5, 5, ExistingEntryBehavior.Ignore));
            Assert.True(result.IsSuccess);
            Assert.Equal("Import complete. Imported 3 records from 1 file", result.Message);

            var records = await collection.Find("{}").ToListAsync();
            Assert.True(records.All(d => d["name"].AsString == "x"));
        }

        [Fact]
        public async Task import_existing_entry_behavior_of_none()
        {
            var importer = new Importer(new MongoSettings(MongoServer));
            var result = await importer.ImportAsync("test", new[] { "./data/exported/animals.json" },
                new ImportSettings(5, 5, ExistingEntryBehavior.None));
            Assert.True(result.IsSuccess);
            Assert.Equal("Import complete. Imported 3 records from 1 file", result.Message);
        }

        [Fact]
        public async Task import_existing_entry_behavior_of_none_throws_exception_on_clash()
        {
            Seed();
            var mongoDb = new MongoClient(Mongo.ConnectionString);
            var collection = mongoDb.GetDatabase("test").GetCollection<BsonDocument>("animals");
            await collection.UpdateManyAsync("{}", BsonDocument.Parse("{$set:{name:'x'}}"));
            var importer = new Importer(new MongoSettings(MongoServer));
            await Assert.ThrowsAsync<ParallelForEachException>(async () =>
            {
                var result = await importer.ImportAsync("test", new[] { "./data/exported/animals.json" },
                    new ImportSettings(5, 5, ExistingEntryBehavior.None));
            });
        }

        [Fact]
        public async Task import_existing_entry_behavior_of_replace()
        {
            Seed();
            var mongoDb = new MongoClient(Mongo.ConnectionString);
            var collection = mongoDb.GetDatabase("test").GetCollection<BsonDocument>("animals");
            await collection.UpdateManyAsync("{}", BsonDocument.Parse("{$set:{name:'x'}}"));
            var importer = new Importer(new MongoSettings(MongoServer));
            var result = await importer.ImportAsync("test", new[] { "./data/exported/animals.json" },
                new ImportSettings(5, 5, ExistingEntryBehavior.Truncate));
            Assert.True(result.IsSuccess);
            Assert.Equal("Import complete. Imported 3 records from 1 file", result.Message);

            var records = await collection.Find("{}").ToListAsync();
            Assert.DoesNotContain(records, d => d["name"].AsString == "x");
        }

        [Fact]
        public async Task import_existing_entry_behavior_of_truncate()
        {
            Seed();
            var importer = new Importer(new MongoSettings(MongoServer));
            var result = await importer.ImportAsync("test", new[] { "./data/exported/animals.json" },
                new ImportSettings(5, 5, ExistingEntryBehavior.Truncate));
            Assert.True(result.IsSuccess);
            Assert.Equal("Import complete. Imported 3 records from 1 file", result.Message);
        }
    }
}