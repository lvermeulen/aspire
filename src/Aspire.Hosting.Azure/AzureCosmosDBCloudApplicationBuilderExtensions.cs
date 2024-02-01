// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure.Data.Cosmos;
using Aspire.Hosting.Publishing;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Azure Cosmos DB resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class AzureCosmosDBCloudApplicationBuilderExtensions
{
    /// <summary>
    /// Adds an Azure Cosmos DB connection to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<AzureCosmosDBResource> AddAzureCosmosDB(
       this IDistributedApplicationBuilder builder,
       string name)
    {
        var connection = new AzureCosmosDBResource(name);
        return builder.AddResource(connection)
                      .WithManifestPublishingCallback(WriteCosmosDBToManifest);
    }

    /// <summary>
    /// Adds a resource which represents a database in the associated Cosmos DB account resource.
    /// </summary>
    /// <param name="builder">AzureCosmosDB resource builder.</param>
    /// <param name="name">Name of database.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<AzureCosmosDBDatabaseResource> AddDatabase(this IResourceBuilder<AzureCosmosDBResource> builder, string name)
    {
        var database = new AzureCosmosDBDatabaseResource(name, builder.Resource);
        return builder.ApplicationBuilder.AddResource(database)
            .WithManifestPublishingCallback(context => WriteCosmosDBDatabaseToManifest(context, database));
    }

    private static void WriteCosmosDBDatabaseToManifest(ManifestPublishingContext context, AzureCosmosDBDatabaseResource database)
    {
        context.Writer.WriteString("type", "azure.cosmosdb.database.v0");
        context.Writer.WriteString("parent", database.Parent.Name);
    }

    private static void WriteCosmosDBToManifest(ManifestPublishingContext context)
    {
        context.Writer.WriteString("type", "azure.cosmosdb.account.v0");
    }
}
