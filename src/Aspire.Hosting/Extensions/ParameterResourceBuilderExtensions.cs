// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting;

public static class ParameterResourceBuilderExtensions
{
    /// <summary>
    /// Adds a parameter resource to the distributed application builder.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="secret">If set to true the parameter is flagged as a secret when emitted to the manifest.</param>
    /// <returns>The resource builder for the parameter resource.</returns>
    public static IResourceBuilder<ParameterResource> AddParameter(this IDistributedApplicationBuilder builder, string name, bool secret = false)
    {
        return builder.AddParameter(name, secret: secret, callback: (context) =>
        {
            var configurationKey = $"Parameters:{name}";
            context.Value = builder.Configuration[configurationKey] ?? throw new DistributedApplicationException($"Parameter resource could not be used because configuration key `{configurationKey}` is missing.");
        });
    }

    internal static IResourceBuilder<ParameterResource> AddParameter(this IDistributedApplicationBuilder builder, string name, Action<ParameterValueCallbackContext> callback, bool secret = false)
    {
        var resource = new ParameterResource(name, callback, secret);
        return builder.AddResource(resource)
                      .WithManifestPublishingCallback((context) => WriteParameterResourceToManifest(context, resource));
    }

    private static void WriteParameterResourceToManifest(ManifestPublishingContext context, ParameterResource resource)
    {
        context.Writer.WriteString("type", "parameter.v0");
        context.Writer.WriteString("value", $"{{{resource.Name}.inputs.value}}");
        context.Writer.WriteStartObject("inputs");
        context.Writer.WriteStartObject("value");
        context.Writer.WriteEndObject();
        context.Writer.WriteEndObject();
    }

    /// <summary>
    /// Adds a connection string parameter resource to the distributed application builder.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the connection string parameter.</param>
    /// <returns>The resource builder for the connection string parameter resource.</returns>
    public static IResourceBuilder<IResourceWithConnectionString> AddConnectionString(this IDistributedApplicationBuilder builder, string name)
    {
        var parameterBuilder = builder.AddParameter(name, (context) =>
        {
            context.Value = builder.Configuration.GetConnectionString(name) ?? throw new DistributedApplicationException($"Connection string parameter resource could not be used because configuration key `ConnectionStrings:{name}` is missing.");
        }, secret: true);

        var surrogate = new ResourceWithConnectionStringSurrogate(parameterBuilder.Resource, (context) =>
        {
            var parameterValueCallbackContext = new ParameterValueCallbackContext(context.ExecutionContext);
            parameterBuilder.Resource.ApplyValue(parameterValueCallbackContext);
            context.ConnectionString = parameterValueCallbackContext.Value;
        });

        return new DistributedApplicationResourceBuilder<IResourceWithConnectionString>(builder, surrogate);
    }
}
