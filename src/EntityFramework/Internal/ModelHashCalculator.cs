﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace System.Data.Entity.Internal
{
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Data.Entity.Infrastructure;
    using System.Data.Entity.Utilities;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using System.Xml;

    // <summary>
    // Calculates the model hash values used the EdmMetadata table from EF 4.1/4.2.
    // </summary>
    internal class ModelHashCalculator
    {
        #region Hash creation

        // <summary>
        // Calculates an SHA256 hash of the EDMX from the given code first model. This is the hash stored in
        // the database in the EdmMetadata table in EF 4.1/4.2. The hash is always calculated using a v2 schema
        // as was generated by EF 4.1/4.2 and with the <see cref="EdmMetadata" /> entity included in the model.
        // </summary>
        public virtual string Calculate(DbCompiledModel compiledModel)
        {
            DebugCheck.NotNull(compiledModel);
            DebugCheck.NotNull(compiledModel.ProviderInfo);
            DebugCheck.NotNull(compiledModel.CachedModelBuilder);

            var providerInfo = compiledModel.ProviderInfo;
            var modelBuilder = compiledModel.CachedModelBuilder.Clone();

            // Add back in the EdmMetadata class because the hash created by EF 4.1 and 4.2 will contain it.
            EdmMetadataContext.ConfigureEdmMetadata(modelBuilder.ModelConfiguration);

            var databaseMetadata = modelBuilder.Build(providerInfo).DatabaseMapping.Database;
            databaseMetadata.SchemaVersion = XmlConstants.StoreVersionForV2; // Ensures SSDL version matches that created by EF 4.1/4.2

            var stringBuilder = new StringBuilder();
            using (var xmlWriter = XmlWriter.Create(
                stringBuilder, new XmlWriterSettings
                    {
                        Indent = true
                    }))
            {
                new SsdlSerializer().Serialize(
                    databaseMetadata,
                    providerInfo.ProviderInvariantName,
                    providerInfo.ProviderManifestToken,
                    xmlWriter);
            }

            return ComputeSha256Hash(stringBuilder.ToString());
        }

        private static string ComputeSha256Hash(string input)
        {
            var hash = GetSha256HashAlgorithm().ComputeHash(Encoding.ASCII.GetBytes(input));

            var builder = new StringBuilder(hash.Length * 2);
            foreach (var bite in hash)
            {
                builder.Append(bite.ToString("X2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static SHA256 GetSha256HashAlgorithm()
        {
            try
            {
                // Use the FIPS compliant SHA256 implementation
                return new SHA256CryptoServiceProvider();
            }
            catch (PlatformNotSupportedException)
            {
                // The FIPS compliant (and faster) algorithm was not available, create the managed version instead.
                // Note: this will throw if FIPS only is enforced.
                return new SHA256Managed();
            }
        }

        #endregion
    }
}
