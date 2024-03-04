// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Grpc.Net.Client;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Stress.ApiService;

public class TelemetryStresser(ILogger<TelemetryStresser> logger, IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var address = config["OTEL_EXPORTER_OTLP_ENDPOINT"]!;
        var channel = GrpcChannel.ForAddress(address);

        var client = new MetricsService.MetricsServiceClient(channel);

        var value = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            value += Random.Shared.Next(0, 10);

            var request = new ExportMetricsServiceRequest
            {
                ResourceMetrics =
                {
                    new ResourceMetrics
                    {
                        Resource = CreateResource("TestResource-<b>Bold</b>", "TestResource-<b>Bold</b>"),
                        ScopeMetrics =
                        {
                            new ScopeMetrics
                            {
                                Scope = new InstrumentationScope
                                {
                                    Name = "TestScope-<b>Bold</b>"
                                },
                                Metrics =
                                {
                                    CreateSumMetric("Test-<b>Bold</b>", DateTime.UtcNow, value: value)
                                }
                            }
                        }
                    }
                }
            };

            logger.LogInformation("Exporting metrics");
            var response = await client.ExportAsync(request, cancellationToken: cancellationToken);
            logger.LogInformation($"Export complete. Rejected count: {response.PartialSuccess?.RejectedDataPoints ?? 0}");

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    public static Resource CreateResource(string? name = null, string? instanceId = null)
    {
        return new Resource()
        {
            Attributes =
            {
                new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = name ?? "TestService" } },
                new KeyValue { Key = "service.instance.id", Value = new AnyValue { StringValue = instanceId ?? "TestId" } }
            }
        };
    }

    public static Metric CreateSumMetric(string metricName, DateTime startTime, KeyValuePair<string, string>[]? attributes = null, int? value = null)
    {
        return new Metric
        {
            Name = metricName,
            Description = "Test metric description",
            Unit = "widget",
            Sum = new Sum
            {
                AggregationTemporality = AggregationTemporality.Cumulative,
                IsMonotonic = true,
                DataPoints =
                {
                    CreateNumberPoint(startTime, value ?? 1, attributes)
                }
            }
        };
    }

    private static NumberDataPoint CreateNumberPoint(DateTime startTime, int value, KeyValuePair<string, string>[]? attributes = null)
    {
        var point = new NumberDataPoint
        {
            AsInt = value,
            StartTimeUnixNano = DateTimeToUnixNanoseconds(startTime),
            TimeUnixNano = DateTimeToUnixNanoseconds(startTime)
        };
        if (attributes != null)
        {
            foreach (var attribute in attributes)
            {
                point.Attributes.Add(new KeyValue { Key = attribute.Key, Value = new AnyValue { StringValue = attribute.Value } });
            }
        }

        return point;
    }

    public static ulong DateTimeToUnixNanoseconds(DateTime dateTime)
    {
        var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var timeSinceEpoch = dateTime.ToUniversalTime() - unixEpoch;

        return (ulong)timeSinceEpoch.Ticks * 100;
    }
}
