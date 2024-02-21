// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Security.Claims;
using Aspire.Dashboard.Authentication;
using Aspire.Dashboard.Authentication.OtlpApiKey;
using Aspire.Dashboard.Authentication.OtlpConnection;
using Aspire.Dashboard.Components;
using Aspire.Dashboard.Model;
using Aspire.Dashboard.Otlp.Grpc;
using Aspire.Dashboard.Otlp.Storage;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Aspire.Dashboard;

public class DashboardWebApplication : IAsyncDisposable
{
    internal const string DashboardOtlpApiKeyVariableName = "DOTNET_DASHBOARD_OTLP_API_KEY";
    internal const string DashboardOtlpUrlVariableName = "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL";
    internal const string DashboardOtlpUrlDefaultValue = "http://localhost:18889";
    internal const string DashboardUrlVariableName = "ASPNETCORE_URLS";
    internal const string DashboardUrlDefaultValue = "http://localhost:18888";

    private readonly WebApplication _app;
    private Func<IPEndPoint>? _browserEndPointAccessor;
    private Func<IPEndPoint>? _otlpServiceEndPointAccessor;

    public Func<IPEndPoint> BrowserEndPointAccessor
    {
        get => _browserEndPointAccessor ?? throw new InvalidOperationException("WebApplication not started yet.");
    }

    public Func<IPEndPoint> OtlpServiceEndPointAccessor
    {
        get => _otlpServiceEndPointAccessor ?? throw new InvalidOperationException("WebApplication not started yet.");
    }

    /// <summary>
    /// Create a new instance of the <see cref="DashboardWebApplication"/> class.
    /// </summary>
    /// <param name="configureBuilder">Configuration the internal app builder. This is for unit testing.</param>
    public DashboardWebApplication(Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder();
        if (configureBuilder != null)
        {
            configureBuilder(builder);
        }

#if !DEBUG
        builder.Logging.AddFilter("Default", LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.None);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Error);
#endif

        var dashboardUris = builder.Configuration.GetUris(DashboardUrlVariableName, new(DashboardUrlDefaultValue));

        var otlpUris = builder.Configuration.GetUris(DashboardOtlpUrlVariableName, new(DashboardOtlpUrlDefaultValue));

        if (otlpUris.Length > 1)
        {
            throw new InvalidOperationException("Only one URL for Aspire dashboard OTLP endpoint is supported.");
        }

        ConfigureKestrelEndpoints(builder, dashboardUris, otlpUris[0]);

        var dashboardHttpsPort = dashboardUris.FirstOrDefault(IsHttps)?.Port;
        var isAllHttps = dashboardHttpsPort is not null && IsHttps(otlpUris[0]);
        if (isAllHttps)
        {
            // Explicitly configure the HTTPS redirect port as we're possibly listening on multiple HTTPS addresses
            // if the dashboard OTLP URL is configured to use HTTPS too
            builder.Services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = dashboardHttpsPort);
        }

        ConfigurationAuthentication(builder);

        // Add services to the container.
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        // Data from the server.
        builder.Services.AddScoped<IDashboardClient, DashboardClient>();

        // OTLP services.
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<TelemetryRepository>();
        builder.Services.AddTransient<StructuredLogsViewModel>();
        builder.Services.AddTransient<TracesViewModel>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutgoingPeerResolver, ResourceOutgoingPeerResolver>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutgoingPeerResolver, BrowserLinkOutgoingPeerResolver>());

        builder.Services.AddFluentUIComponents();

        builder.Services.AddSingleton<ThemeManager>();

        builder.Services.AddLocalization();

        _app = builder.Build();

        var logger = _app.Logger;

        if (dashboardUris.FirstOrDefault() is { } reportedDashboardUri)
        {
            // dotnet watch needs the trailing slash removed. See https://github.com/dotnet/sdk/issues/36709
            logger.LogInformation("Now listening on: {DashboardUri}", reportedDashboardUri.AbsoluteUri.TrimEnd('/'));
        }

        if (otlpUris.FirstOrDefault() is { } reportedOtlpUri)
        {
            // This isn't used by dotnet watch but still useful to have for debugging
            logger.LogInformation("OTLP server running at: {OtlpEndpointUri}", reportedOtlpUri.AbsoluteUri.TrimEnd('/'));
        }

        // Redirect browser directly to /StructuredLogs address if the dashboard is running without a resource service.
        // This is done to avoid immediately navigating in the Blazor app.
        _app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals(TargetLocationInterceptor.ResourcesPath, StringComparisons.UrlPath))
            {
                var client = context.RequestServices.GetRequiredService<IDashboardClient>();
                if (!client.IsEnabled)
                {
                    context.Response.Redirect(TargetLocationInterceptor.StructuredLogsPath);
                    return;
                }
            }

            await next(context).ConfigureAwait(false);
        });

        // Configure the HTTP request pipeline.
        if (_app.Environment.IsDevelopment())
        {
            _app.UseDeveloperExceptionPage();
            //_app.UseBrowserLink();
        }
        else
        {
            _app.UseExceptionHandler("/Error");
            //_app.UseHsts();
        }

        if (isAllHttps)
        {
            _app.UseHttpsRedirection();
        }

        _app.UseStaticFiles(new StaticFileOptions()
        {
            OnPrepareResponse = context =>
            {
                // If Cache-Control isn't already set to something, set it to 'no-cache' so that the
                // ETag and Last-Modified headers will be respected by the browser.
                // This may be able to be removed if https://github.com/dotnet/aspnetcore/issues/44153
                // is fixed to make this the default
                if (context.Context.Response.Headers.CacheControl.Count == 0)
                {
                    context.Context.Response.Headers.CacheControl = "no-cache";
                }
            }
        });

        _app.UseAuthorization();

        _app.UseAntiforgery();

        _app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        // OTLP gRPC services.
        _app.MapGrpcService<OtlpMetricsService>();
        _app.MapGrpcService<OtlpTraceService>();
        _app.MapGrpcService<OtlpLogsService>();
    }

    // Kestrel endpoints are loaded from configuration. This is done so that advanced configuration of endpoints is
    // possible from the caller. e.g., using environment variables to configure each endpoint's TLS certificate.
    private void ConfigureKestrelEndpoints(WebApplicationBuilder builder, Uri[] dashboardUris, Uri otlpUri)
    {
        // Translate high-level config settings such as DOTNET_DASHBOARD_OTLP_ENDPOINT_URL and ASPNETCORE_URLS
        // to Kestrel's schema for loading endpoints from configuration.
        var browserEndpointNames = new List<string>();
        var initialValues = new Dictionary<string, string?>();
        AddEndpointConfiguration(initialValues, "Otlp", otlpUri.OriginalString, HttpProtocols.Http2);
        if (dashboardUris.Length == 1)
        {
            browserEndpointNames.Add("Browser");
            AddEndpointConfiguration(initialValues, "Browser", dashboardUris[0].OriginalString);
        }
        else
        {
            for (var i = 0; i < dashboardUris.Length; i++)
            {
                var name = $"Browser{i}";
                browserEndpointNames.Add(name);
                AddEndpointConfiguration(initialValues, name, dashboardUris[i].OriginalString);
            }
        }

        static void AddEndpointConfiguration(Dictionary<string, string?> values, string endpointName, string url, HttpProtocols? protocols = null)
        {
            values[$"Kestrel:Endpoints:{endpointName}:Url"] = url;
            if (protocols != null)
            {
                values[$"Kestrel:Endpoints:{endpointName}:Protocols"] = protocols.ToString();
            }
        }

        builder.Configuration.AddInMemoryCollection(initialValues);

        // Use ConfigurationLoader to augment the endpoint's Kestrel created from configuration
        // with extra settings. e.g., UseOtlpConnection for the OTLP endpoint.
        builder.WebHost.ConfigureKestrel((context, serverOptions) =>
        {
            var kestrelSection = context.Configuration.GetSection("Kestrel");

            var configurationLoader = serverOptions.Configure(kestrelSection);

            foreach (var browserEndpointName in browserEndpointNames)
            {
                configurationLoader.Endpoint(browserEndpointName, endpointConfiguration =>
                {
                    // Only the last endpoint is accessible. Tests should only need one but
                    // this will need to be improved if that changes.
                    _browserEndPointAccessor = CreateEndPointAccessor(endpointConfiguration.ListenOptions);
                });
            }

            configurationLoader.Endpoint("Otlp", endpointConfiguration =>
            {
                _otlpServiceEndPointAccessor = CreateEndPointAccessor(endpointConfiguration.ListenOptions);
                endpointConfiguration.ListenOptions.UseOtlpConnection();
            });
        });

        static Func<IPEndPoint> CreateEndPointAccessor(ListenOptions options)
        {
            // We want to provide a way for someone to get the IP address of an endpoint.
            // However, if a dynamic port is used, the port is not known until the server is started.
            // Instead of returning the ListenOption's endpoint directly, we provide a func that returns the endpoint.
            // The endpoint on ListenOptions is updated after binding, so accessing it via the func after the server
            // has started returns the resolved port.
            return () => options.IPEndPoint!;
        }
    }

    private static void ConfigurationAuthentication(WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(OtlpCompositeAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<OtlpCompositeAuthenticationHandlerOptions, OtlpCompositeAuthenticationHandler>(OtlpCompositeAuthenticationDefaults.AuthenticationScheme, o => { })
            .AddScheme<OtlpApiKeyAuthenticationHandlerOptions, OtlpApiKeyAuthenticationHandler>(OtlpApiKeyAuthenticationDefaults.AuthenticationScheme, o => { })
            .AddScheme<OtlpConnectionAuthenticationHandlerOptions, OtlpConnectionAuthenticationHandler>(OtlpConnectionAuthenticationDefaults.AuthenticationScheme, o => { })
            .AddCertificate(options =>
            {
                options.Events = new CertificateAuthenticationEvents
                {
                    OnCertificateValidated = context =>
                    {
                        var claims = new[]
                        {
                            new Claim(
                                ClaimTypes.NameIdentifier,
                                context.ClientCertificate.Subject,
                                ClaimValueTypes.String, context.Options.ClaimsIssuer),
                            new Claim(
                                ClaimTypes.Name,
                                context.ClientCertificate.Subject,
                                ClaimValueTypes.String, context.Options.ClaimsIssuer)
                                };

                        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, context.Scheme.Name));
                        context.Success();

                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(OtlpAuthorization.PolicyName, policy =>
            {
                policy.RequireClaim(OtlpAuthorization.OtlpClaimName);
            });
        });

        var otlpApiKey = builder.Configuration[DashboardOtlpApiKeyVariableName];
        if (!string.IsNullOrEmpty(otlpApiKey))
        {
            builder.Services.AddSingleton<IApiKeyValidation>(new ApiKeyValidation(otlpApiKey));
        }
        else
        {
            builder.Services.AddSingleton<IApiKeyValidation, DisabledApiKeyValidation>();
        }
    }

    public void Run() => _app.Run();

    public Task StartAsync(CancellationToken cancellationToken = default) => _app.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) => _app.StopAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        return _app.DisposeAsync();
    }

    private static bool IsHttps(Uri uri) => string.Equals(uri.Scheme, "https", StringComparison.Ordinal);
}
