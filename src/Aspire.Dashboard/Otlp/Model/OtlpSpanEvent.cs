// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Dashboard.Otlp.Model;

public class OtlpSpanEvent
{
    public required string Name { get; init; }
    public required DateTime Time { get; init; }
    public required ReadOnlyMemory<KeyValuePair<string, string>> Attributes { get; init; }
    public TimeSpan TimeOffset(OtlpSpan span) => (Time - span.StartTime);
}
