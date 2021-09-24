using System;
using System.IO;
using System.Threading.Tasks;
using Mongo2Go;
using Xunit;

namespace Xtensible.MangoSeed.Core.Tests
{
    public class ExportTests : IDisposable
    {
        public ExportTests()
        {
            Mongo = MongoDbRunner.Start();
            Mongo.Import("test", "animals", "./data/animals.json", true);
        }

        private MongoDbRunner Mongo { get; }
        private string MongoServer => Mongo.ConnectionString.Replace("mongodb://", "").Replace("/", "");

        public void Dispose()
        {
            Mongo.Dispose();
        }

        [Fact]
        public async Task export_all_documents_in_collection()
        {
            var exporter = new Exporter(new MongoSettings(MongoServer));
            using var sw = new StreamWriter(new MemoryStream());
            var result = await exporter.ExportAsync("test", "animals", "{}", sw.BaseStream);
            Assert.True(result.IsSuccess);
            Assert.Equal("Export complete. Exported 3 documents", result.Message);
        }

        [Fact]
        public async Task export_documents_using_query()
        {
            var exporter = new Exporter(new MongoSettings(MongoServer));
            using var sw = new StreamWriter(new MemoryStream());
            var result = await exporter.ExportAsync("test", "animals", "{name:'Lucky'}", sw.BaseStream);
            Assert.True(result.IsSuccess);
            Assert.Equal("Export complete. Exported 1 document", result.Message);
        }
    }
}