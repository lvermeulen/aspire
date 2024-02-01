// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a MySQL database. This is a child resource of a <see cref="MySqlContainerResource"/>.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="mySqlParentResource">The MySQL parent resource associated with this database.</param>
public class MySqlDatabaseResource(string name, IMySqlParentResource mySqlParentResource) : Resource(name), IResourceWithParent<IMySqlParentResource>, IResourceWithConnectionString
{
    public IMySqlParentResource Parent { get; } = mySqlParentResource;

    /// <summary>
    /// Gets the connection string for the MySQL database.
    /// </summary>
    /// <returns>A connection string for the MySQL database.</returns>
    public void EvaluateConnectionString(ConnectionStringCallbackContext context)
    {
        Parent.EvaluateConnectionString(context);
        if (context.ConnectionString is { } connectionString)
        {
            context.ConnectionString = $"{connectionString}Database={Name}";
        }
        else
        {
            throw new DistributedApplicationException("Parent resource connection string was null.");
        }
    }
}
