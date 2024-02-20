// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authorization;

namespace Aspire.Dashboard.Authentication;

public class ApiKeyAuthorizationHandler : AuthorizationHandler<ApiKeyAuthorizationRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IApiKeyValidation _apiKeyValidation;

    public ApiKeyAuthorizationHandler(IHttpContextAccessor httpContextAccessor, IApiKeyValidation apiKeyValidation)
    {
        _httpContextAccessor = httpContextAccessor;
        _apiKeyValidation = apiKeyValidation;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ApiKeyAuthorizationRequirement requirement)
    {
        var apiKey = _httpContextAccessor?.HttpContext?.Request.Headers[ApiKeyDefaults.ApiKeyHeaderName].ToString();
        if (!_apiKeyValidation.IsValidApiKey(apiKey))
        {
            context.Fail();
            return Task.CompletedTask;
        }
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
