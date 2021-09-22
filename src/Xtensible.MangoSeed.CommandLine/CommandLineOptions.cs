using CommandLine;
using Xtensible.MangoSeed.Core;

namespace Xtensible.MangoSeed.CommandLine
{
    public class SharedOptions
    {
        [Option("no-logo", Required = false, HelpText = "Skip logo", Default = false)]
        public bool SkipLogo { get; set; }

        [Option('u', "user", Required = false, HelpText = "Username")]
        public string? User { get; set; }

        [Option('p', "password", Required = false,
            HelpText = "Password.  Alternatively use environment variable MANGOSEED_DB_PW=<password>")]
        public string? Password { get; set; }

        [Option("authentication-db", Required = false, HelpText = "Authentication database", Default = "admin")]
        public string? AuthenticationDb { get; set; }

        [Option("authentication-mechanism", Required = false,
            HelpText = "Authentication mechanism.  Options: SCRAM-SHA-1 or SCRAM-SHA-256", Default = "SCRAM-SHA-1")]
        public string? AuthenticationMechanism { get; set; }

        [Option('s', "server", Required = false, HelpText = "Server", Default = "127.0.0.1:27017")]
        public string? Server { get; set; }

        [Option('t', "tls-enabled", Required = false, HelpText = "Use TLS", Default = false)]
        public bool TlsEnabled { get; set; }

        [Option('d', "db", Required = true, HelpText = "Mongo Database to use")]
        public string Database { get; set; } = null!;
    }

    [Verb("export")]
    public class ExportOptions : SharedOptions
    {
        [Option('c', "collection", Required = true, HelpText = "The source collection")]
        public string Collection { get; set; } = null!;

        [Option('q', "query", SetName = "query", Required = true,
            HelpText = "ExportAsync query. example: {name:'Harry'}")]
        public string Query { get; set; } = null!;

        [Option("destination", Required = false, HelpText = "Destination path")]
        public string? Destination { get; set; }

        [Option("disable-pretty-print", Required = false, HelpText = "When disabled, json output is not indented")]
        public bool DisablePrettyPrint { get; set; }
    }

    [Verb("import", true)]
    public class ImportOptions : SharedOptions
    {
        [Option('f', "seed-file-path", Required = true,
            HelpText = "Seed path. Can be single json file or directory containing json seed files")]
        public string? Source { get; set; }

        [Option("max-dop", Required = false, HelpText = "Number of files that will be processed at the same time",
            Default = 4)]
        public int MaxDegreeOfParallelism { get; set; }

        [Option("truncate", SetName = "truncate", Required = false,
            HelpText =
                "Truncates the collection if it exists, but keeps the indexes.  Can be slow if the collection is large",
            Default = false)]
        public bool Truncate { get; set; }

        [Option("drop", SetName = "truncate", Required = false,
            HelpText =
                "Drops the collection if it exists, including indexes.  Fast, but indexes will need to be recreated outside of MangoSeed",
            Default = false)]
        public bool Drop { get; set; }

        [Option("batch-size", Required = false,
            HelpText =
                "Number of documents to batch when inserting.  Use a higher number for small documents and a smaller number for very large documents",
            Default = 500)]
        public int BatchSize { get; set; }

        [Option("existing-entry-behavior", Required = false,
            HelpText =
                "Action to take when an entry with a given ID already exists.  When truncating or dropping table, behavior will always be Nothing, which is the fastest setting.  Setting Nothing when there is a clash will result in an error.\n\nPlease note that all data mutations are not transactional.",
            Default = ExistingEntryBehavior.None)]
        public ExistingEntryBehavior ExistingEntryBehavior { get; set; }
    }
}