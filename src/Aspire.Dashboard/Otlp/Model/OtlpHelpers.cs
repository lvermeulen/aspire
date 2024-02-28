// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Aspire.Dashboard.Otlp.Storage;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Aspire.Dashboard.Otlp.Model;

public static class OtlpHelpers
{
    public static string? GetServiceId(this Resource resource)
    {
        string? serviceName = null;

        for (var i = 0; i < resource.Attributes.Count; i++)
        {
            var attribute = resource.Attributes[i];
            if (attribute.Key == OtlpApplication.SERVICE_INSTANCE_ID)
            {
                return attribute.Value.GetString();
            }
            if (attribute.Key == OtlpApplication.SERVICE_NAME)
            {
                serviceName = attribute.Value.GetString();
            }
        }

        //
        // NOTE: The service.instance.id value is a recommended attribute, but not required.
        //       See: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/README.md#service-experimental
        //
        return serviceName;
    }

    public static string ToShortenedId(string id) => TruncateString(id, maxLength: 7);

    public static string ToHexString(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        // This produces lowercase hex string from the bytes. It's used instead of Convert.ToHexString()
        // because we want to display lowercase hex string in the UI for values such as traceid and spanid.
        return string.Create(bytes.Length * 2, bytes, static (chars, bytes) =>
        {
            var data = bytes.Span;
            for (var pos = 0; pos < data.Length; pos++)
            {
                ToCharsBuffer(data[pos], chars, pos * 2);
            }
        });
    }

    public static string TruncateString(string value, int maxLength)
    {
        return value.Length > maxLength ? value[..maxLength] : value;
    }

    public static string ToHexString(this ByteString bytes)
    {
        return ToHexString(bytes.Memory);
    }

    public static string GetString(this AnyValue value) =>
        value.ValueCase switch
        {
            AnyValue.ValueOneofCase.StringValue => value.StringValue,
            AnyValue.ValueOneofCase.IntValue => value.IntValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue.ToString(CultureInfo.InvariantCulture),
            AnyValue.ValueOneofCase.BoolValue => value.BoolValue ? "true" : "false",
            AnyValue.ValueOneofCase.BytesValue => value.BytesValue.ToHexString(),
            _ => value.ToString(),
        };

    // From https://github.com/dotnet/runtime/blob/963954a11c1beeea4ad63002084a213d8d742864/src/libraries/Common/src/System/HexConverter.cs#L81-L89
    // Modified slightly to always produce lowercase output.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ToCharsBuffer(byte value, Span<char> buffer, int startingIndex = 0)
    {
        var difference = ((value & 0xF0U) << 4) + (value & 0x0FU) - 0x8989U;
        var packedResult = (((uint)-(int)difference & 0x7070U) >> 4) + difference + 0xB9B9U | 0x2020U;

        buffer[startingIndex + 1] = (char)(packedResult & 0xFF);
        buffer[startingIndex] = (char)(packedResult >> 8);
    }

    public static DateTime UnixNanoSecondsToDateTime(ulong unixTimeNanoseconds)
    {
        var ticks = NanosecondsToTicks(unixTimeNanoseconds);

        return DateTime.UnixEpoch.AddTicks(ticks);
    }

    private static long NanosecondsToTicks(ulong nanoseconds)
    {
        return (long)(nanoseconds / TimeSpan.NanosecondsPerTick);
    }

    public static ReadOnlyMemory<KeyValuePair<string, string>> ToKeyValuePairs(this RepeatedField<KeyValue> attributes, TelemetryOptions options, Func<KeyValue, bool>? filter = null)
    {
        if (attributes.Count == 0)
        {
            return Array.Empty<KeyValuePair<string, string>>();
        }

        var values = new KeyValuePair<string, string>[Math.Min(attributes.Count, options.AttributeCountLimit)];
        CopyKeyValues(attributes, values, options, filter, out var copyCount);

        return values.AsMemory(0, copyCount);
    }

    public static void CopyKeyValuePairs(RepeatedField<KeyValue> attributes, TelemetryOptions options, out int copyCount, [NotNull] ref Memory<KeyValuePair<string, string>>? copiedAttributes)
    {
        var readCount = Math.Min(attributes.Count, options.AttributeCountLimit);

        if (copiedAttributes is null || copiedAttributes.Value.Length < readCount)
        {
            copiedAttributes = new KeyValuePair<string, string>[readCount];
        }
        else
        {
            copiedAttributes.Value.Span.Clear();
        }

        CopyKeyValues(attributes, copiedAttributes.Value, options, filter: null, out copyCount);
    }

    private static void CopyKeyValues(RepeatedField<KeyValue> attributes, Memory<KeyValuePair<string, string>> copiedAttributes, TelemetryOptions options, Func<KeyValue, bool>? filter, out int copyCount)
    {
        var a = copiedAttributes.Span;
        var readCount = Math.Min(attributes.Count, options.AttributeCountLimit);
        var currentIndex = 0;
        for (var i = 0; i < readCount; i++)
        {
            var attribute = attributes[i];

            if (filter != null && !filter(attribute))
            {
                continue;
            }

            var value = TruncateString(attribute.Value.GetString(), options.AttributeLengthLimit);

            a[currentIndex++] = new KeyValuePair<string, string>(attribute.Key, value);
        }
        copyCount = currentIndex;
    }

    public static string? GetValue(this ReadOnlyMemory<KeyValuePair<string, string>> values, string name)
    {
        var v = values.Span;
        for (var i = 0; i < v.Length; i++)
        {
            if (v[i].Key == name)
            {
                return v[i].Value;
            }
        }
        return null;
    }

    public static string? GetPeerAddress(this ReadOnlyMemory<KeyValuePair<string, string>> values)
    {
        var address = GetValue(values, OtlpSpan.PeerServiceAttributeKey);
        if (address != null)
        {
            return address;
        }

        // OTEL HTTP 1.7.0 doesn't return peer.service. Fallback to server.address and server.port.
        if (GetValue(values, OtlpSpan.ServerAddressAttributeKey) is { } server)
        {
            if (GetValue(values, OtlpSpan.ServerPortAttributeKey) is { } serverPort)
            {
                server += ":" + serverPort;
            }
            return server;
        }

        // Fallback to older names of net.peer.name and net.peer.port.
        if (GetValue(values, OtlpSpan.NetPeerNameAttributeKey) is { } peer)
        {
            if (GetValue(values, OtlpSpan.NetPeerPortAttributeKey) is { } peerPort)
            {
                peer += ":" + peerPort;
            }
            return peer;
        }

        return null;
    }

    public static bool HasKey(this ReadOnlyMemory<KeyValuePair<string, string>> values, string name)
    {
        var v = values.Span;
        for (var i = 0; i < v.Length; i++)
        {
            if (v[i].Key == name)
            {
                return true;
            }
        }
        return false;
    }

    public static string ConcatProperties(this ReadOnlyMemory<KeyValuePair<string, string>> properties)
    {
        StringBuilder sb = new();
        var first = true;
        foreach (var kv in properties.Span)
        {
            if (!first)
            {
                sb.Append(", ");
            }
            first = false;
            sb.Append(CultureInfo.InvariantCulture, $"{kv.Key}: ");
            sb.Append(string.IsNullOrEmpty(kv.Value) ? "\'\'" : kv.Value);
        }
        return sb.ToString();
    }

    public static PagedResult<T> GetItems<T>(IEnumerable<T> results, int startIndex, int? count)
    {
        return GetItems<T, T>(results, startIndex, count, null);
    }

    public static PagedResult<TResult> GetItems<TSource, TResult>(IEnumerable<TSource> results, int startIndex, int? count, Func<TSource, TResult>? select)
    {
        var query = results.Skip(startIndex);
        if (count != null)
        {
            query = query.Take(count.Value);
        }
        List<TResult> items;
        if (select != null)
        {
            items = query.Select(select).ToList();
        }
        else
        {
            items = query.Cast<TResult>().ToList();
        }
        var totalItemCount = results.Count();

        return new PagedResult<TResult>
        {
            Items = items,
            TotalItemCount = totalItemCount
        };
    }
}
