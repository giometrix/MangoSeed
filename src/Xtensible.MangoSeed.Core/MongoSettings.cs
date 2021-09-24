namespace Xtensible.MangoSeed.Core
{
    /// <summary>
    /// Options for connecting to MongoDb
    /// </summary>
    /// <param name="Server">The host. Can include port</param>
    /// <param name="AuthenticationMechanism">Authentication options for connecting to MongoDb <see href="https://docs.mongodb.com/manual/core/security-scram/"/></param>
    /// <param name="UseTls"></param>
    /// <param name="AllowInsecureTls"></param>
    /// <param name="Username"></param>
    /// <param name="Password"></param>
    /// <param name="AuthenticationDatabase">Which database to authenticate with</param>
    public record MongoSettings(string Server, string? AuthenticationMechanism = "SCRAM-SHA-1", bool UseTls = false,
        bool AllowInsecureTls = false, string? Username = null, string? Password = null, string? AuthenticationDatabase = null);
}