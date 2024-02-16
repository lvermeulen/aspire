// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;

namespace Aspire.Hosting.Azure.Bicep;

internal class AzureBicepExtensions
{
    public static void WriteBicepResourceToManifest(ManifestPublishingContext context, IResource resource)
    {
        _ = resource;
        context.Writer.WriteString("type", "azure.bicep.v0");
    }
}
