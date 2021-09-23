using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Xtensible.MangoSeed.Core;
using static Crayon.Output;

namespace Xtensible.MangoSeed.CommandLine
{
    internal class Program
    {
        private const string MongoSeedPasswordEnvVariable = "MANGOSEED_DB_PW";
        private const int UserErrorExitCode = 1;
        private const int UnexpectedErrorExitCode = 2;
        private static readonly Func<string, string> Mango = text => Rgb(0xff, 0x88, 0x30).Text(text);
        private static readonly Func<string, string> VibrantGreen = text => Rgb(0x66, 0xff, 0x00).Text(text);

        private async static Task Main(string[] args)
        {
            var parser = new Parser(x =>
            {
                x.CaseInsensitiveEnumValues = true;
                x.AutoHelp = true;
                x.AutoVersion = true;
                x.EnableDashDash = true;
                x.HelpWriter = Console.Out;
            });


            var result = parser.ParseArguments<ImportOptions, ExportOptions>(args);

            result.WithNotParsed(errors =>
            {
                HelpText helpText = HelpText.AutoBuild(result);
                helpText.AddEnumValuesToHelpText = true;
                helpText.AddOptions(result);

                string msg = Environment.NewLine + helpText;

                Console.WriteLine(msg);
            });


            if (result.Tag == ParserResultType.Parsed)
            {
                Info(
                    $"Starting MangoSeed v{Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion}...",
                    String.Empty);

                MongoSettings mongoSettings = default!;
                await result.WithParsedAsync<SharedOptions>(async options =>
                {
                    mongoSettings = GetMongoSettings(options);
                    Logo(options.SkipLogo);
                });

                await result.WithParsedAsync<ImportOptions>(async options =>
                {
                    string section = "Import";

                    Info(section, "Beginning import...");

                    var elapsedTime = await TimedOperation(async () =>
                    {
                        var source = options.Source;
                        if (Directory.Exists($"{source}"))
                        {
                            Info(section, $"{source} is a directory, recursively processing all .json files ...");
                            var files = Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories);
                            await ProcessImport(mongoSettings, files, options, section);
                        }
                        else if (File.Exists($"{source}"))
                        {
                            Info(section, $"{source} is a file, processing...");
                            await ProcessImport(mongoSettings, new[] { source }!, options, section);
                        }
                        else
                        {
                            Error(section, $"{source} does not exist.  Exiting...");
                            Environment.Exit(UserErrorExitCode);
                        }
                    });
                    Info(section, elapsedTime);
                });

                await result.WithParsedAsync<ExportOptions>(async options =>
                {
                    string section = "Export";
                    Info(section, "Beginning export...");
                    var destination = Path.Combine(Environment.CurrentDirectory, $"{options.Collection}.json");
                    if (!String.IsNullOrEmpty(options.Destination))
                    {
                        destination = options.Destination.Trim();
                    }

                    var exporter = new Exporter(mongoSettings);
                    var elapsedTime = await TimedOperation(async () =>
                    {
                        try
                        {
                            Info(section,
                                $"Querying {options.Server}/{options.Database}/{options.Collection} with query {options.Query}");
                            await using var fileStream = File.CreateText(destination);
                            var exportResult = await exporter.ExportAsync(options.Database, options.Collection, options.Query,
                                fileStream.BaseStream, GetExportSettings(options));
                            await fileStream.FlushAsync();
                            Log(section, exportResult);
                            if (exportResult.IsSuccess)
                            {
                                Info(section, $"Results written to {destination}");
                            }
                            else
                            {
                                Environment.Exit(UserErrorExitCode);
                            }
                        }
                        catch (Exception e)
                        {
                            Error(section, e.Message);
                            Environment.Exit(UnexpectedErrorExitCode);
                        }
                    });
                    Info(section, elapsedTime);
                });
            }
        }

        private async static Task ProcessImport(MongoSettings mongoSettings, IEnumerable<string> files,
            ImportOptions options, string section)
        {
            var importer = new Importer(mongoSettings);
            try
            {
                var result = await importer.ImportAsync(options.Database, files, GetImportSettings(options),
                    r =>
                    {
                        Log(section, r);
                    });

                Log(section, result);
            }
            catch (Exception e)
            {
                Error(section, e.Message);
                Environment.Exit(UnexpectedErrorExitCode);
            }
        }

        private static MongoSettings GetMongoSettings(SharedOptions options)
        {
            string? password = null;
            var username = options.User?.Trim();
            if (!String.IsNullOrEmpty(username))
            {
                password = options.Password;
                if (String.IsNullOrEmpty(password))
                {
                    password = Environment.GetEnvironmentVariable(MongoSeedPasswordEnvVariable);
                }
            }

            var server = options.Server?.Trim();
            if (String.IsNullOrEmpty(server))
            {
                Error("Connection", "Missing password or authentication db");
                Environment.Exit(UserErrorExitCode);
            }

            var authDb = options.AuthenticationDb?.Trim();
            if (!String.IsNullOrEmpty(username))
            {
                if (String.IsNullOrEmpty(password) || String.IsNullOrEmpty(authDb))
                {
                    Error("Connection", "Missing password or authentication db");
                    Environment.Exit(UserErrorExitCode);
                }
            }

            var authMechanism = options.AuthenticationMechanism?.Trim();
            var useTls = options.TlsEnabled;
            var allowInsecureTls = options.AllowInsecureTls;

            return new MongoSettings(server, authMechanism, useTls, allowInsecureTls, username, password, authDb);
        }

        private static ExportSettings GetExportSettings(ExportOptions exportOptions)
        {
            return new ExportSettings(!exportOptions.DisablePrettyPrint);
        }

        private static ImportSettings GetImportSettings(ImportOptions options)
        {
            return new ImportSettings(options.BatchSize, options.MaxDegreeOfParallelism, options.ExistingEntryBehavior);
        }

        private static void Log(string section, Result result)
        {
            if (result.IsSuccess)
            {
                Info(section, result.Message);
            }
            else
            {
                Error(section, result.Message);
            }
        }

        private static void Info(string section, string info)
        {
            Console.WriteLine(Bold(Mango(section)) + " " + VibrantGreen(info));
        }

        private static void Info(string section, TimeSpan elapsedTime)
        {
            var time = elapsedTime.TotalMilliseconds switch {
                < 1_000 => $"{elapsedTime.TotalMilliseconds}ms",
                >= 1_000 and < 60_000 => $"{elapsedTime.TotalSeconds}s",
                >= 60_000 and < 3_600_000 => $"{elapsedTime.TotalMinutes}m",
                _ => $"{elapsedTime.TotalHours}h"
            };
            Info(section, $"Took {time} to complete");
        }

        private static void Error(string section, string error)
        {
            Console.Error.WriteLine(Mango(section) + " " + Red(error));
        }

        private static void Logo(bool skip)
        {
            if (skip)
            {
                return;
            }

            string leaf = @"
                                    ......                                             
                                .-=+*###%%%%%%%%##**+=-:.                                 
                            .-+###########%%%%%%%%%%%%%%##+-.                             
                         .-=++++*********####################*+=:                         
                       :=+++++******+**************************##*=:                      
                     .-==+*****#*####*###*##########*#**************+-.                   
                   :=++==-----==++*##############**+++**************#**+-.                
                 :+:.::::::::::::----=++***########**+=-::.           .::--:";
            string mango = @"
              -..%::::::::::-------------==+++****#####**+=-:.                            
             :#%*%-:::::--------------------====+++*********+=-:.                         
               -+::::--------======-==----=========+++++*****++==-:.                      
             ..::::-------============================+++++++++++===:.                    
            :::------========================================++++++===-.                  
          .:-------==========++++===+=========================++++++++=-:.                
        .:-------============+++++++++++=================+=+++++++++=====-:               
       .:-----========+==+++++++++++++++++++++==========++++++++++++=======-.             
       :-=======+=====+++++++++++++++++++++++++++++++=+++++++++++++++=======-:            
      .--=======++===+++++++++++++++++++++++++++++++++++++++++++++++++=======-:           
      :-========++++++++++++++++++++++++++++++++++++++++++++++++++++++++=====--:          
      :-======++++++++++++++++++++++++++++++++++++++++++++++++++++++++++==+====-:         
      :-=====+++++++++++++++*++++++++++++++++++++++++++++++++++++++++++++++=====-:        
      :-====++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++=====-:       
      .-====+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++=======-.      
       :=====++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++========:      
        -=====+++++++++++++++++++++++++++++++++++++=+++++++++++++++++++++++++======-      
        .--======++++++++++++++++++++++++++++++===++=+++++++++++++++++++++++++++====.     
          :-=======++===+==++==+++++++++++++++++==+++++++++++++++++++++++++++++++===:     
           :-=======+==+=======++++++++++++++++++++++++++++++++++++++++++++++++++===:     
            .-=========+===++++++++++++++++++++++++++++++++++++++++++++++++++++++===:     
              :-==========++++++++++++===+++++++++++++++++++++++++++++++++++++++++==.     
               .:-============+++++++++++==++=++++++++++++++++++++++++++**+++++++==:      
                 .:-===========++=+++++++++++++++++++++++++++++++++++++++***+++++=-       
                    .:-==========+++++++++++++++++++++++++++++++++++++**++++++++=-        
                       .:-=+++++++++++++++++++++++++++++++++++++++++++**+**++++=:         
                          .:-=+++++++++++++***++++++++++++++++++++++++*****+++=:.         
                        ..:::--==+++***************************++++++******++-:..         
                                :-----====+++++********############*****...         
";
            Console.WriteLine(VibrantGreen(leaf) + Mango(mango));
        }

        private async static Task<TimeSpan> TimedOperation(Func<Task> operation)
        {
            var stopwatch = Stopwatch.StartNew();
            await operation();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }
    }
}