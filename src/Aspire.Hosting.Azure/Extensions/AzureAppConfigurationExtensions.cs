// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning.AppConfiguration;
using Azure.Provisioning.Authorization;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding the Azure AppConfiguration resources to the application model.
/// </summary>
public static class AzureAppConfigurationExtensions
{
    /// <summary>
    /// Adds an Azure App Configuration resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<AzureAppConfigurationResource> AddAzureAppConfiguration(this IDistributedApplicationBuilder builder, string name)
    {
        var resource = new AzureAppConfigurationResource(name);
        return builder.AddResource(resource)
                .WithParameter("configName", resource.CreateBicepResourceName())
                .WithParameter(AzureBicepResource.KnownParameters.PrincipalId)
                .WithParameter(AzureBicepResource.KnownParameters.PrincipalType)
                .WithManifestPublishingCallback(resource.WriteToManifest);
    }

    /// <summary>
    /// Adds an Azure App Configuration resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="configureResource"></param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<AzureAppConfigurationConstructResource> AddAzureAppConfigurationConstruct(this IDistributedApplicationBuilder builder, string name, Action<IResourceBuilder<AzureAppConfigurationConstructResource>, ResourceModuleConstruct, AppConfigurationStore>? configureResource = null)
    {
        var configureConstruct = (ResourceModuleConstruct construct) =>
        {
            var store = new AppConfigurationStore(construct, name: name, skuName: "standard");
            store.AddOutput("appConfigEndpoint", x => x.Endpoint);
            var appConfigurationDataOwnerRoleAssignemnt = store.AssignRole(RoleDefinition.AppConfigurationDataOwner);
            appConfigurationDataOwnerRoleAssignemnt.AssignProperty(x => x.PrincipalId, construct.PrincipalIdParameter);
            appConfigurationDataOwnerRoleAssignemnt.AssignProperty(x => x.PrincipalType, construct.PrincipalTypeParameter);

            store.Properties.Tags["aspire-resource-name"] = construct.Resource.Name;

            if (configureResource != null)
            {
                var resource = (AzureAppConfigurationConstructResource)construct.Resource;
                var resourceBuilder = builder.CreateResourceBuilder(resource);
                configureResource(resourceBuilder, construct, store);
            }
        };

        var resource = new AzureAppConfigurationConstructResource(name, configureConstruct);
        return builder.AddResource(resource)
                      .WithParameter(AzureBicepResource.KnownParameters.PrincipalId)
                      .WithParameter(AzureBicepResource.KnownParameters.PrincipalType)
                      .WithManifestPublishingCallback(resource.WriteToManifest);
    }
}
