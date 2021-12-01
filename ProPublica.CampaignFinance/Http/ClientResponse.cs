using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ProPublica.CampaignFinance.Http
{
    public class ClientResponse
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        });

        private readonly JObject _token;

        public ClientResponse(JObject token)
        {
            _token = token;
        }

        public IEnumerable<string> GetErrors()
        {
            return _token.SelectToken("errors/error")?.Select(error => error.ToObject<string>(Serializer)) ??
                   Enumerable.Empty<string>();
        }

        public string GetMessage()
        {
            return _token.SelectToken("message")?.ToObject<string>(Serializer);
        }

        public ClientResponseStatus GetStatus()
        {
            return Enum.TryParse<ClientResponseStatus>(_token.SelectToken("status")?.ToObject<string>(Serializer), true,
                out var status)
                ? status
                : ClientResponseStatus.Error;
        }

        public IEnumerable<T> Many<T>(string path = null) where T : class
        {
            var results = _token.SelectToken("results");

            JToken token;

            switch (results?.Type)
            {
                case JTokenType.Array:
                    token = path != null ? results.Single().SelectToken(path) : results;
                    break;
                default:
                    throw new NotSupportedException($"Unable to traverse token ({results}.)");
            }

            try
            {
                return token?.Select(result => result.ToObject<T>(Serializer)) ?? Enumerable.Empty<T>();
            }
            catch (JsonReaderException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public T One<T>(string path = null) where T : class
        {
            var results = _token.SelectToken("results");

            JToken token;

            switch (results?.Type)
            {
                case JTokenType.Array:
                    token = path != null ? results.Single().SelectToken(path) : results.Single();
                    break;
                case JTokenType.Object:
                    token = path != null ? results.SelectToken(path) : results;
                    break;
                default:
                    throw new NotSupportedException($"Unable to traverse token ({results}.)");
            }

            try
            {
                return token?.ToObject<T>(Serializer);
            }
            catch (JsonReaderException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}