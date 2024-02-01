// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a parameter resource.
/// </summary>
public sealed class ParameterResource : Resource
{
    private readonly Action<ParameterValueCallbackContext> _callback;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterResource"/> class.
    /// </summary>
    /// <param name="name">The name of the parameter resource.</param>
    /// <param name="secret">A flag indicating whether the parameter is secret.</param>
    /// <param name="callback">The callback function to retrieve the parameter value.</param>
    internal ParameterResource(string name, Action<ParameterValueCallbackContext> callback, bool secret) : base(name)
    {
        Secret = secret;
        _callback = callback;
    }

    /// <summary>
    /// Gets a value indicating whether the parameter is secret.
    /// </summary>
    public bool Secret { get; }

    internal void EvaluateValue(ParameterValueCallbackContext context) => _callback(context);
}
