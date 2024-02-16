// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure.Bicep;

namespace Aspire.Hosting.Azure.Redis;

/// <summary>
/// TODO: Doc comments.
/// </summary>
public static class AzureRedisExtensions
{
    /// <summary>
    /// TODO: Doc comments.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IResourceBuilder<RedisResource> PublishAsAzureCacheForRedis(this IResourceBuilder<RedisResource> builder)
    {
        return builder.WithManifestPublishingCallback(context => AzureBicepExtensions.WriteBicepResourceToManifest(context, builder.Resource));
    }
}
