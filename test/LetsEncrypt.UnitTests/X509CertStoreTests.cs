﻿using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using McMaster.AspNetCore.LetsEncrypt;
using McMaster.AspNetCore.LetsEncrypt.Internal;
using McMaster.Extensions.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace LetsEncrypt.UnitTests
{
    using static TestUtils;

    public class X509CertStoreTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly LetsEncryptOptions _options;
        private readonly X509CertStore _certStore;

        public X509CertStoreTests(ITestOutputHelper output)
        {
            _output = output;
            _options = new LetsEncryptOptions();
            _certStore = new X509CertStore(Options.Create(_options), NullLogger<X509CertStore>.Instance)
            {
                AllowInvalidCerts = true
            };
        }

        public void Dispose()
        {
            _certStore.Dispose();
        }

        [SkippableFact]
        [SkipOnAzurePipelinesWindows(SkipReason = "On Windows in Azure Pipelines, adding certs to store doesn't work for unclear reasons.")]
        public async Task ItFindsCertByCommonNameAsync()
        {
            var commonName = "x509store.read.letsencrypt.test.natemcmaster.com";
            _options.DomainNames = new[] { commonName };
            using var x509store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            x509store.Open(OpenFlags.ReadWrite);
            var testCert = CreateTestCert(commonName);
            x509store.Add(testCert);

            _output.WriteLine($"Adding cert {testCert.Thumbprint} to My/CurrentUser");

            try
            {
                var certs = await _certStore.GetCertificatesAsync(default);
                var foundCert = Assert.Single(certs);
                Assert.NotNull(foundCert);
                Assert.Equal(testCert, foundCert);
            }
            finally
            {

                x509store.Remove(testCert);
            }
        }

        [SkippableFact]
        [SkipOnAzurePipelinesWindows(SkipReason = "On Windows in Azure Pipelines, adding certs to store doesn't work for unclear reasons.")]
        public async Task ItSavesCertificates()
        {
            var commonName = "x509store.save.letsencrypt.test.natemcmaster.com";
            var testCert = CreateTestCert(commonName);
            using var x509store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            x509store.Open(OpenFlags.ReadWrite);

            try
            {
                await _certStore.SaveAsync(testCert, default);

                var certificates = x509store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    testCert.Thumbprint,
                    validOnly: false);

                _output.WriteLine($"Searching for cert {testCert.Thumbprint} to My/CurrentUser");

                var foundCert = Assert.Single(certificates);

                Assert.NotNull(foundCert);
                Assert.Equal(testCert, foundCert);
            }
            finally
            {
                x509store.Remove(testCert);
            }
        }

        [Fact]
        public async Task ItReturnsEmptyWhenCantFindCertAsync()
        {
            var commonName = "notfound.letsencrypt.test.natemcmaster.com";
            _options.DomainNames = new[] { commonName };
            var certs = await _certStore.GetCertificatesAsync(default);
            Assert.Empty(certs);
        }
    }
}
