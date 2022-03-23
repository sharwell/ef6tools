// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace EFDesigner.IntegrationTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using EFDesigner.IntegrationTests.InProcess;
    using Microsoft.Data.Entity.Design.VisualStudio.Package;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Xunit;

    public class SafeModeTestsRemote : AbstractIntegrationTest
    {
        private readonly EFArtifactHelper _efArtifactHelper =
            new EFArtifactHelper(EFArtifactHelper.GetEntityDesignModelManager(ServiceProvider.GlobalProvider));

        private IEdmPackage _package;
        private readonly (string DeploymentDirectory, string unused) TestContext = (Path.GetDirectoryName(typeof(AutomaticDbContextTests).Assembly.Location), "");

        public override async Task InitializeAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            await base.InitializeAsync();

            PackageManager.LoadEDMPackage(ServiceProvider.GlobalProvider);
            _package = PackageManager.Package;
        }

        private string ModelsDirectory
        {
            get { return Path.Combine(TestContext.DeploymentDirectory, @"TestData\InProc"); }
        }

        private string ModelValidationDirectory
        {
            get { return Path.Combine(TestContext.DeploymentDirectory, @"TestData\Model\ValidationSamples"); }
        }

        [IdeFact]
        public async Task PubsModelWithXmlParserErrors()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelsDirectory, "SafeModeTests.PubsModelWithXmlParserErrors.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task PubsModelWithXSDErrors()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelsDirectory, "SafeModeTests.PubsModelWithXSDErrors.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task PubsModelWithEdmxXSDErrors()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelsDirectory, "SafeModeTests.PubsModelWithEdmxXSDErrors.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task PubsMinimalWithFunctionMapping()
        {
            Assert.True(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelsDirectory, "PubsMinimalWithFunctionMapping.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task PubsMinimalWithFunctionMappingV2()
        {
            Assert.True(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelsDirectory, "PubsMinimalWithFunctionMappingV2.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task PubsMinimalWithFunctionMappingAndEdmxXsdError()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelsDirectory, "SafeModeTests.PubsMinimalWithFunctionMappingAndEDMXSchemaError.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task UndefinedComplexPropertyType()
        {
            Assert.True(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelsDirectory, "UndefinedComplexPropertyType.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task CircularInheritance()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelValidationDirectory, "CircularInheritance.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task EntityTypeWithNoEntitySet()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelValidationDirectory, "EntityTypeWithNoEntitySet.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task MultipleEntitySetsPerType()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelValidationDirectory, "MultipleEntitySetsPerType.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task AssociationWithoutAssociationSet()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    Path.Combine(ModelValidationDirectory, "AssociationWithoutAssociationSet.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedEdmxTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{http://schemas.microsoft.com/ado/2007/06/edmx}Edmx",
                        "NonQualifiedEdmxTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedDesignerTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{http://schemas.microsoft.com/ado/2007/06/edmx}Designer",
                        "NonQualifiedDesignerTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedRuntimeTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{http://schemas.microsoft.com/ado/2007/06/edmx}Runtime",
                        "NonQualifiedRuntimeTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedMappingsTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{http://schemas.microsoft.com/ado/2007/06/edmx}Mappings",
                        "NonQualifiedMappingsTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedConceptualModelsTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{http://schemas.microsoft.com/ado/2007/06/edmx}ConceptualModels",
                        "NonQualifiedConceptualModelsTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedStorageModelsTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{http://schemas.microsoft.com/ado/2007/06/edmx}StorageModels",
                        "NonQualifiedStorageModelsTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedMappingTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{urn:schemas-microsoft-com:windows:storage:mapping:CS}Mapping",
                        "NonQualifiedMappingTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedConceptualSchemaTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{http://schemas.microsoft.com/ado/2006/04/edm}Schema",
                        "NonQualifiedConceptualSchemaTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        [IdeFact]
        public async Task NonQualifiedStorageSchemaTag()
        {
            Assert.False(
                await IsArtifactDesignerSafeAsync(
                    GenerateInvalidEdmx(
                        "{http://schemas.microsoft.com/ado/2006/04/edm/ssdl}Schema",
                        "NonQualifiedStorageSchemaTag.edmx"),
                    HangMitigatingCancellationToken));
        }

        private string GenerateInvalidEdmx(XName elementName, string destinationPath)
        {
            var sourceModel = XDocument.Load(Path.Combine(ModelsDirectory, "PubsMinimal.edmx"));
            var elementToChange = sourceModel.Descendants(elementName).Single();
            elementToChange.Name = "{http://tempuri.org}" + elementToChange.Name.LocalName;
            sourceModel.Save(destinationPath);

            return destinationPath;
        }

        // TODO: How much value these add? Maybe just need to be removed
        [IdeFact]
        public void LoadInvalidSampleFiles()
        {
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "TwoThree.EmptySet.cs.edmx", false); // contains error 2063
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "EdmRally.edmx", false); // contains error 3023
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "AbstractSimpleMappingError1.edmx", false); // contains error 2078
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "ComplexType.Condition.edmx", true); // contains error 2016
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "EntitySplitting_Same_EdmProperty_Maps_Different_Store_Type.edmx", false); // contains error 2039
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "EntitySplitting_Same_EdmProperty_Maps_Same_Store_Type_Non_Promotable_Facets.edmx", false); // contains error 2039
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "ExtraIllegalElement.edmx", true); // contains error 102, 2025
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "MemberTypeMismatch.CS.edmx", false); // contains error 2007,2063
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "NullableComplexType.edmx", true); // contains error 157, 2002
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "TwoThree.InvalidXml2.edmx", true); // contains error 102, 2025
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "TwoThree.PartialMapping.StorageEntityContainerMismatch1.edmx", true); // contains error 2007, 2063, also has a MaxLength schema error
            LoadFileTest(TestContextEntityDesigner.GeneratedEdmxInvalidSamplesDirectory, "UnparsableQueryView.edmx", true); // contains error 2068
        }

        private void LoadFileTest(string directory, string fileName, bool _)
        {
            throw new NotImplementedException();
        }

        private async Task<bool> IsArtifactDesignerSafeAsync(string fileName, CancellationToken cancellationToken)
        {
            var isArtifactDesignerSafe = true;

            var artifactUri = TestUtils.FileName2Uri(fileName);
            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);

            try
            {
                dte.OpenFile(artifactUri.LocalPath);
                isArtifactDesignerSafe = _efArtifactHelper.GetNewOrExistingArtifact(artifactUri).IsDesignerSafe;
            }
            finally
            {
                dte.CloseDocument(fileName, false);
            }

            return isArtifactDesignerSafe;
        }

        private static class TestContextEntityDesigner
        {
            public static string GeneratedEdmxInvalidSamplesDirectory => throw new NotImplementedException();
        }
    }
}
