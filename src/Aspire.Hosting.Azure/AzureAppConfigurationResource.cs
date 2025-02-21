// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Azure;

/// <summary>
/// A resource that represents Azure App Configuration.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class AzureAppConfigurationResource(string name) :
    AzureBicepResource(name, templateResourceName: "Aspire.Hosting.Azure.Bicep.appconfig.bicep"),
    IResourceWithConnectionString
{
    /// <summary>
    /// Gets the appConfigEndpoint output reference for the Azure App Configuration resource.
    /// </summary>
    public BicepOutputReference Endpoint => new("appConfigEndpoint", this);

    /// <summary>
    /// Gets the connection string template for the manifest for the Azure App Configuration resource.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Endpoint}");
}

/// <summary>
/// A resource that represents Azure App Configuration.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="configureConstruct"></param>
public class AzureAppConfigurationConstructResource(string name, Action<ResourceModuleConstruct> configureConstruct) :
    AzureConstructResource(name, configureConstruct),
    IResourceWithConnectionString
{
    /// <summary>
    /// Gets the appConfigEndpoint output reference for the Azure App Configuration resource.
    /// </summary>
    public BicepOutputReference Endpoint => new("appConfigEndpoint", this);

    /// <summary>
    /// Gets the connection string template for the manifest for the Azure App Configuration resource.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
       ReferenceExpression.Create($"{Endpoint}");
}
