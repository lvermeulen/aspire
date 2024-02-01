// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

internal sealed class ResourceWithConnectionStringSurrogate(IResource innerResource, Action<ConnectionStringCallbackContext> callback) : IResourceWithConnectionString
{
    public string Name => innerResource.Name;

    public ResourceMetadataCollection Annotations => innerResource.Annotations;

    public void ApplyConnectionString(ConnectionStringCallbackContext context) => callback(context);
}
