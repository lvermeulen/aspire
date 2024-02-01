// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting;

public class DistributedApplicationExecutionContext(DistributedApplicationOperation operation)
{
    public DistributedApplicationOperation Operation { get; } = operation;
}
