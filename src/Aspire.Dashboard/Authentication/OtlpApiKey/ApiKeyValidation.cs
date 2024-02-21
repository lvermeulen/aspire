// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Authentication.OtlpApiKey;

public class ApiKeyValidation : IApiKeyValidation
{
    private readonly string _apiKey;

    public ApiKeyValidation(string apiKey)
    {
        _apiKey = apiKey;
    }

    public bool IsValidApiKey(string? apiKey)
    {
        return apiKey == _apiKey;
    }
}

public class DisabledApiKeyValidation : IApiKeyValidation
{
    public bool IsValidApiKey(string? apiKey)
    {
        return true;
    }
}
