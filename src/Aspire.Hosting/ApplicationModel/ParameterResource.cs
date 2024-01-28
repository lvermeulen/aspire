// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

public class ParameterResource(string name, bool secret) : Resource(name)
{
    public bool IsSecret { get; } = secret;
}
