// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.ServiceDiscovery.Internal;

namespace Microsoft.Extensions.ServiceDiscovery.Abstractions;

/// <summary>
/// A service endpoint resolver that uses configuration to resolve endpoints.
/// </summary>
internal sealed partial class ConfigurationServiceEndPointResolver : IServiceEndPointProvider, IHostNameFeature
{
    private const string DefaultEndPointName = "default";
    private readonly string _serviceName;
    private readonly string? _endpointName;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationServiceEndPointResolver> _logger;
    private readonly IOptions<ConfigurationServiceEndPointResolverOptions> _options;

    /// <summary>
    /// Initializes a new <see cref="ConfigurationServiceEndPointResolver"/> instance.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
    public ConfigurationServiceEndPointResolver(
        string serviceName,
        IConfiguration configuration,
        ILogger<ConfigurationServiceEndPointResolver> logger,
        IOptions<ConfigurationServiceEndPointResolverOptions> options)
    {
        if (ServiceNameParts.TryParse(serviceName, out var parts))
        {
            _serviceName = parts.Host;
            _endpointName = parts.EndPointName;
        }
        else
        {
            throw new InvalidOperationException($"Service name '{serviceName}' is not valid.");
        }

        _configuration = configuration;
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    /// <inheritdoc/>
    public ValueTask<ResolutionStatus> ResolveAsync(ServiceEndPointCollectionSource endPoints, CancellationToken cancellationToken) => new(ResolveInternal(endPoints));

    string IHostNameFeature.HostName => _serviceName;

    private ResolutionStatus ResolveInternal(ServiceEndPointCollectionSource endPoints)
    {
        // Only add endpoints to the collection if a previous provider (eg, an override) did not add them.
        if (endPoints.EndPoints.Count != 0)
        {
            Log.SkippedResolution(_logger, _serviceName, "Collection has existing endpoints");
            return ResolutionStatus.None;
        }

        var endpointName = _endpointName ?? DefaultEndPointName;
        var root = _configuration.GetSection(_options.Value.SectionName);

        // Get the corresponding config section.
        var section = root.GetSection(_serviceName);
        if (!section.Exists())
        {
            return CreateNotFoundResponse(endPoints, $"{_options.Value.SectionName}:{_serviceName}");
        }

        var endpointSection = section.GetSection(endpointName);
        if (!endpointSection.Exists())
        {
            if (section.GetChildren().All(c => int.TryParse(c.Key, out _) && !string.IsNullOrWhiteSpace(c.Value)))
            {
                return GetLegacyEndPoints(endPoints, section, endpointName);
            }

            return CreateNotFoundResponse(endPoints, $"{_options.Value.SectionName}:{_serviceName}:{endpointName}");
        }

        endPoints.AddChangeToken(endpointSection.GetReloadToken());

        var configPath = $"{_options.Value.SectionName}:{_serviceName}:{endpointName}";
        Log.UsingConfigurationPath(_logger, configPath, endpointName, _serviceName);

        // Account for both the single and multi-value cases.
        if (!string.IsNullOrWhiteSpace(endpointSection.Value))
        {
            if (!TryAddEndPoint(endPoints, endpointSection, endpointName, out var error))
            {
                return error;
            }
        }
        else
        {
            foreach (var child in endpointSection.GetChildren())
            {
                if (!int.TryParse(child.Key, out _))
                {
                    return ResolutionStatus.FromException(new KeyNotFoundException($"The endpoint configuration section for service '{_serviceName}' endpoint '{endpointName}' has non-numeric keys."));
                }

                if (!TryAddEndPoint(endPoints, child, endpointName, out var error))
                {
                    return error;
                }
            }
        }

        if (endPoints.EndPoints.Count == 0)
        {
            Log.ConfigurationNotFound(_logger, _serviceName, configPath);
        }

        return ResolutionStatus.Success;

    }

    private bool TryAddEndPoint(ServiceEndPointCollectionSource endPoints, IConfigurationSection section, string endpointName, out ResolutionStatus error)
    {
        var value = section.Value;
        if (string.IsNullOrWhiteSpace(value) || !TryParseEndPoint(value, out var endPoint))
        {
            error = ResolutionStatus.FromException(new KeyNotFoundException($"The endpoint configuration section for service '{_serviceName}' endpoint '{endpointName}' has an invalid value with key '{section.Key}'."));
            return false;
        }

        endPoints.EndPoints.Add(CreateEndPoint(endPoint));
        error = default;
        return true;
    }

    private static bool TryParseEndPoint(string value, [NotNullWhen(true)] out EndPoint? endPoint)
    {
        if (value.IndexOf("://") < 0 && Uri.TryCreate($"fakescheme://{value}", default, out var uri))
        {
            var port = uri.Port > 0 ? uri.Port : 0;
            if (IPAddress.TryParse(uri.Host, out var ip))
            {
                endPoint = new IPEndPoint(ip, port);
            }
            else
            {
                endPoint = new DnsEndPoint(uri.Host, port);
            }
        }
        else if (Uri.TryCreate(value, default, out uri))
        {
            endPoint = new UriEndPoint(uri);
        }
        else
        {
            endPoint = null;
            return false;
        }

        return true;
    }

    private ResolutionStatus GetLegacyEndPoints(ServiceEndPointCollectionSource endPoints, IConfigurationSection section, string endpointName)
    {
        endPoints.AddChangeToken(section.GetReloadToken());
        var configPath = $"{_options.Value.SectionName}:{_serviceName}";
        Log.UsingConfigurationPath(_logger, configPath, endpointName, _serviceName);

        // Read the endpoint from the configuration.
        // First check if there is a collection of sections
        var children = section.GetChildren();
        if (children.Any())
        {
            var values = children.Select(c => c.Value!).Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (values is { Count: > 0 })
            {
                // Use endpoint names if any of the values have an endpoint name set.
                var parsedValues = ParseServiceNameParts(values, configPath);
                Log.ConfiguredEndPoints(_logger, _serviceName, configPath, parsedValues);

                var matchEndPointNames = !parsedValues.TrueForAll(static uri => string.IsNullOrEmpty(uri.EndPointName));
                Log.EndPointNameMatchSelection(_logger, _serviceName, matchEndPointNames);

                foreach (var uri in parsedValues)
                {
                    // If either endpoint names are not in-use or the scheme matches, create an endpoint for this value.
                    if (!matchEndPointNames || EndPointNamesMatch(_endpointName, uri))
                    {
                        if (!ServiceNameParts.TryCreateEndPoint(uri, out var endPoint))
                        {
                            return ResolutionStatus.FromException(new KeyNotFoundException($"The endpoint configuration section for service '{_serviceName}' is invalid."));
                        }

                        endPoints.EndPoints.Add(CreateEndPoint(endPoint));
                    }
                }
            }
        }
        else if (section.Value is { } value && ServiceNameParts.TryParse(value, out var parsed))
        {
            if (EndPointNamesMatch(_endpointName, parsed))
            {
                if (!ServiceNameParts.TryCreateEndPoint(parsed, out var endPoint))
                {
                    return ResolutionStatus.FromException(new KeyNotFoundException($"The endpoint configuration section for service '{_serviceName}' is invalid."));
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    Log.ConfiguredEndPoints(_logger, _serviceName, configPath, [parsed]);
                }

                endPoints.EndPoints.Add(CreateEndPoint(endPoint));
            }
        }

        if (endPoints.EndPoints.Count == 0)
        {
            Log.ConfigurationNotFound(_logger, _serviceName, configPath);
        }

        return ResolutionStatus.Success;

        static bool EndPointNamesMatch(string? endPointName, ServiceNameParts parts) =>
            string.IsNullOrEmpty(parts.EndPointName)
            || string.IsNullOrEmpty(endPointName)
            || string.Equals(parts.EndPointName, endPointName, StringComparison.OrdinalIgnoreCase);
    }

    private ServiceEndPoint CreateEndPoint(EndPoint endPoint)
    {
        var serviceEndPoint = ServiceEndPoint.Create(endPoint);
        serviceEndPoint.Features.Set<IServiceEndPointProvider>(this);
        if (_options.Value.ApplyHostNameMetadata(serviceEndPoint))
        {
            serviceEndPoint.Features.Set<IHostNameFeature>(this);
        }

        return serviceEndPoint;
    }

    private ResolutionStatus CreateNotFoundResponse(ServiceEndPointCollectionSource endPoints, string configPath)
    {
        endPoints.AddChangeToken(_configuration.GetReloadToken());
        Log.ConfigurationNotFound(_logger, _serviceName, configPath);
        return ResolutionStatus.CreateNotFound($"No configuration for the specified path '{_options.Value.SectionName}:{_serviceName}' was found.");
    }

    private List<ServiceNameParts> ParseServiceNameParts(List<string> input, string configPath)
    {
        var results = new List<ServiceNameParts>(input.Count);
        for (var i = 0; i < input.Count; ++i)
        {
            if (ServiceNameParts.TryParse(input[i], out var value))
            {
                if (!results.Contains(value))
                {
                    results.Add(value);
                }
            }
            else
            {
                throw new InvalidOperationException($"The endpoint configuration '{input[i]}' from path '{configPath}[{i}]' for service '{_serviceName}' is invalid.");
            }
        }

        return results;
    }

    public override string ToString() => "Configuration";
}
