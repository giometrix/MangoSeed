namespace Xtensible.MangoSeed.Core
{
    public record MongoSettings(string Server, string? AuthenticationMechanism = "SCRAM-SHA-1", bool UseTls = false,
        bool AllowInsecureTls = false, string? Username = null, string? Password = null, string? AuthenticationDatabase = null);
}