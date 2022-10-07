﻿using System.Runtime.CompilerServices;
using Coding.Blog.Engine.Configurations;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

[assembly: InternalsVisibleTo("Coding.Blog.UnitTests")]
namespace Coding.Blog.Engine.Clients;

internal sealed class CosmicClient<T> : ICosmicClient<T>
{
    private readonly CosmicConfiguration _configuration;
    private readonly ILogger<T> _logger;
    private readonly IAsyncPolicy<T> _resiliencePolicy;
    private readonly string _baseUrl;

    public CosmicClient(
        IOptions<CosmicConfiguration> configurationOptions,
        ILogger<T> logger,
        IAsyncPolicy<T> resiliencePolicy)
    {
        _configuration = configurationOptions.Value;
        _logger = logger;
        _resiliencePolicy = resiliencePolicy;
        _baseUrl = $"{_configuration.Endpoint}/buckets/{_configuration.BucketSlug}/objects";
    }

    public async Task<T> GetAsync()
    {
        var typeName = typeof(T).FullName;
        var (type, props) = CosmicRequestRegistry.Requests[typeName!];

        try
        {
            return await _resiliencePolicy.ExecuteAsync(
                async _ => await _baseUrl
                    .SetQueryParam("query", $"{{\"type\":\"{type}\"}}")
                    .SetQueryParam("read_key", _configuration.ReadKey)
                    .SetQueryParam("props", props)
                    .GetJsonAsync<T>().ConfigureAwait(false),
                new Context(typeName)
            ).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError($"Failed to retrieve {typeName} from Cosmic API: {exception.Message}");

            throw;
        }
    }
}