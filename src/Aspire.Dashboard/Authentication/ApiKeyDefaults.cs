// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Authentication;

public static class ApiKeyDefaults
{
    public const string AuthenticationScheme = "ApiKey";
    public const string PolicyName = "ApiKeyPolicy";

    public const string ApiKeyHeaderName = "x-otlp-api-key";
}
