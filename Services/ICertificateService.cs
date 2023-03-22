// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Security.Cryptography.X509Certificates;

namespace GraphNotifications.Services
{
    public interface ICertificateService
    {
        Task<X509Certificate2> GetDecryptionCertificate();
        Task<X509Certificate2> GetEncryptionCertificate();
    }
}