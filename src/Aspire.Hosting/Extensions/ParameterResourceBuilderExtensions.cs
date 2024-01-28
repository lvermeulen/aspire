// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Aspire.Hosting;

public static class ParameterResourceBuilderExtensions
{
    public static IResourceBuilder<ParameterResource> AddParameter(this IDistributedApplicationBuilder builder, string name, bool secret = false)
    {
        var resource = new ParameterResource(name, secret);
        return builder.AddResource(resource)
                      .WithManifestPublishingCallback(context => WriteSecretToManifest(context, resource));
    }

    public static IResourceBuilder<ParameterResource> FromConnectionString(this IResourceBuilder<ParameterResource> builder, string connectionStringName)
    {
        return builder.WithAnnotation(new ConnectionStringCallbackAnnotation(context =>
        {
            var connectionString = builder.ApplicationBuilder.Configuration.GetConnectionString(connectionStringName);
            context.ConnectionString = connectionString ?? throw new DistributedApplicationException($"Connection string '{connectionStringName}' not found.");
        }));
    }

    private static void WriteSecretToManifest(ManifestPublishingContext context, ParameterResource resource)
    {
        context.Writer.WriteString("type", "parameter.v0");
        context.Writer.WriteString("value", $"{{{resource.Name}.inputs.value}}");
        context.Writer.WriteStartObject("inputs");
        context.Writer.WriteStartObject("value");
        context.Writer.WriteString("type", "string");
        if (resource.IsSecret)
        {
            context.Writer.WriteBoolean("secret", true);
        }
        context.Writer.WriteEndObject();
        context.Writer.WriteEndObject();
    }

    public static IResourceBuilder<T> WithEnvironment<T>(this IResourceBuilder<T> builder, string name, IResourceBuilder<ParameterResource> parameter) where T: IResourceWithEnvironment
    {
        return builder.WithEnvironment(context =>
        {
            if (context.PublisherName == "manifest")
            {
                context.EnvironmentVariables[name] = $"{{{parameter.Resource.Name}.value}}";
                return;
            }

            var configurationKey = $"Secrets:{parameter.Resource.Parent.Name}:{secret.Resource.Name}";

            context.EnvironmentVariables[name] = builder.ApplicationBuilder.Configuration[configurationKey]
                ?? throw new DistributedApplicationException($"Environment variable '{name}' could not be added because configuration key '{configurationKey}' not present.");
        });
    }
}
