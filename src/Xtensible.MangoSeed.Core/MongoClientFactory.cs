using System;
using MongoDB.Driver;

namespace Xtensible.MangoSeed.Core
{
    /// <summary>
    ///     Creates mongo clients with the provided options
    /// </summary>
    internal static class MongoClientFactory
    {
        public static MongoClient Create(MongoSettings settings)
        {
            var server = settings.Server.Split(':');
            if (server.Length != 2 || !Int32.TryParse(server[1], out _))
            {
                throw new InvalidOperationException($"{settings.Server} is not a valid address");
            }

            var mongoClientSettings = new MongoClientSettings {
                Server = new MongoServerAddress(server[0], Int32.Parse(server[1])),
                ApplicationName = "MangoSeed",
                UseTls = settings.UseTls,
                AllowInsecureTls = settings.AllowInsecureTls
            };

            if (!String.IsNullOrEmpty(settings.Username))
            {
                var identity = new MongoInternalIdentity(settings.AuthenticationDatabase, settings.Username);
                var evidence = new PasswordEvidence(settings.Password);
                mongoClientSettings.Credential =
                    new MongoCredential(settings.AuthenticationMechanism, identity, evidence);
            }

            return new MongoClient(mongoClientSettings);
        }
    }
}