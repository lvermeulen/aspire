// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Authentication.OtlpApiKey;

public interface IApiKeyValidation
{
    bool IsValidApiKey(string? apiKey);
}
