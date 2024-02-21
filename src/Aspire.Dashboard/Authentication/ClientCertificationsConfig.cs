// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Certificate;

namespace Aspire.Dashboard.Authentication;

public class ClientCertificationsConfig
{
    public CertificateTypes? AllowedCertificateTypes { get; set; }

    public X509ChainTrustMode? ChainTrustValidationMode { get; set; }

    public bool? ValidateCertificateUse { get; set; }

    public bool? ValidateValidityPeriod { get; set; }

    public X509RevocationFlag? RevocationFlag { get; set; }

    public X509RevocationMode? RevocationMode { get; set; }
}
