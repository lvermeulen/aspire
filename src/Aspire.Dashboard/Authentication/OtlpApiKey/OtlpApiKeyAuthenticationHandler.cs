// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Aspire.Dashboard.Authentication.OtlpApiKey;

public class OtlpApiKeyAuthenticationHandler : AuthenticationHandler<OtlpApiKeyAuthenticationHandlerOptions>
{
    public const string ApiKeyHeaderName = "x-otlp-api-key";

    private readonly IApiKeyValidation _apiKeyValidation;

    public OtlpApiKeyAuthenticationHandler(IApiKeyValidation apiKeyValidation, IOptionsMonitor<OtlpApiKeyAuthenticationHandlerOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
        _apiKeyValidation = apiKeyValidation;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var apiKey = Context.Request.Headers[ApiKeyHeaderName].ToString();
        if (!_apiKeyValidation.IsValidApiKey(apiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Incoming API key doesn't match required API key."));
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }
}

public static class OtlpApiKeyAuthenticationDefaults
{
    public const string AuthenticationScheme = "OtlpApiKey";
}

public sealed class OtlpApiKeyAuthenticationHandlerOptions : AuthenticationSchemeOptions
{
}
