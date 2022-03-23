// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace EFDesigner.IntegrationTests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using EFDesigner.IntegrationTests.InProcess;
    using Microsoft.Data.Entity.Design.Model;
    using Microsoft.Data.Entity.Design.VisualStudio.Package;
    using Microsoft.VisualStudio.Extensibility.Testing;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.Win32;
    using Xunit;

    public class MultiTargetingTestsInProcRemote : AbstractIntegrationTest
    {
        private IEdmPackage _package;

        public override async Task InitializeAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            await base.InitializeAsync();

            PackageManager.LoadEDMPackage(ServiceProvider.GlobalProvider);
            _package = PackageManager.Package;
        }

        private readonly EFArtifactHelper _efArtifactHelper =
            new EFArtifactHelper(EFArtifactHelper.GetEntityDesignModelManager(ServiceProvider.GlobalProvider));

        private enum FrameworkVersion
        {
            V30,
            V35,
            V40
        };

        private readonly IDictionary<FrameworkVersion, string> _mapFameworkRegistryPath;

        public MultiTargetingTestsInProcRemote()
        {
            _mapFameworkRegistryPath = new Dictionary<FrameworkVersion, string>();
            _mapFameworkRegistryPath[FrameworkVersion.V35] = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5";
            _mapFameworkRegistryPath[FrameworkVersion.V40] = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4.0";
        }

        private class ArtifactStatus
        {
            public readonly bool IsDesignerSafe;
            public readonly bool IsStructurallySafe;
            public readonly bool IsVersionSafe;

            public ArtifactStatus(bool isDesignerSafe, bool isStructurallySafe, bool isVersionSafe)
            {
                IsDesignerSafe = isDesignerSafe;
                IsStructurallySafe = isStructurallySafe;
                IsVersionSafe = isVersionSafe;
            }
        }

        [IdeFact]
        public async Task MultiTargeting40ProjectFileOpen()
        {
            if (!NETFrameworkVersionInstalled(FrameworkVersion.V40))
            {
                TestContext.WriteLine(
                    TestContext.TestName + "skipped - .NET Framework " + FrameworkVersion.V40.ToString("G") + " not installed.");
                return;
            }

            await TestProjectMultiTargetingAsync(
                "MT40Project",
                "TestMultiTargeting40Project",
                new Dictionary<FrameworkVersion, ArtifactStatus>
                    {
                        { FrameworkVersion.V40, new ArtifactStatus(isDesignerSafe: true, isStructurallySafe: true, isVersionSafe: true) },
                        { FrameworkVersion.V35, new ArtifactStatus(isDesignerSafe: false, isStructurallySafe: true, isVersionSafe: false) }
                    },
                HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task MultiTargeting35ProjectFileOpen()
        {
            if (!NETFrameworkVersionInstalled(FrameworkVersion.V35))
            {
                TestContext.WriteLine(
                    TestContext.TestName + "skipped - .NET Framework " + FrameworkVersion.V35.ToString("G") + " not installed.");
                return;
            }

            await TestProjectMultiTargetingAsync(
                "MT35Project",
                "TestMultiTargeting35Project",
                new Dictionary<FrameworkVersion, ArtifactStatus>
                    {
                        { FrameworkVersion.V40, new ArtifactStatus(isDesignerSafe: false, isStructurallySafe: true, isVersionSafe: false) },
                        { FrameworkVersion.V35, new ArtifactStatus(isDesignerSafe: true, isStructurallySafe: true, isVersionSafe: true) }
                    },
                HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task MultiTargeting30ProjectFileOpen()
        {
            // we can't use registry to determine whether net fx 3.0
            if (!NETFrameworkVersionInstalled(FrameworkVersion.V35))
            {
                TestContext.WriteLine(
                    TestContext.TestName + "skipped - .NET Framework " + FrameworkVersion.V35.ToString("G") + " not installed.");
                return;
            }

            await TestProjectMultiTargetingAsync(
                "MT30Project",
                "TestMultiTargeting30Project",
                new Dictionary<FrameworkVersion, ArtifactStatus>
                    {
                        { FrameworkVersion.V40, new ArtifactStatus(isDesignerSafe: false, isStructurallySafe: true, isVersionSafe: false) },
                        { FrameworkVersion.V35, new ArtifactStatus(isDesignerSafe: false, isStructurallySafe: true, isVersionSafe: false) }
                    },
                HangMitigatingCancellationToken);
        }

        private async Task TestProjectMultiTargetingAsync(
            string projectName, string testName, IDictionary<FrameworkVersion, ArtifactStatus> expectedResults, CancellationToken cancellationToken)
        {
            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);

            var projectDir = Path.Combine(TestContext.DeploymentDirectory, @"TestData\InProc\MultiTargeting", projectName);
            var solnFilePath = Path.Combine(projectDir, projectName + ".sln");
            try
            {
                dte.OpenSolution(solnFilePath);

                // We need to wait until the project loading events are processed
                // Otherwise, we could get into a state where we open the EDMX file
                // into the miscellaneous files project

                EnvDTE.Project project;
                var attempts = 100;
                do
                {
                    project = dte.FindProject(projectName);
                    await InProcComponent.WaitForApplicationIdleAsync(cancellationToken);
                    if (attempts-- < 0)
                    {
                        Assert.True(false, "Cannot open solution");
                    }
                }
                while (project == null);

                await TestLoadingArtifactAsync(project, projectDir + @"\NorthwindModel_40.edmx", expectedResults[FrameworkVersion.V40], cancellationToken);
                await TestLoadingArtifactAsync(project, projectDir + @"\NorthwindModel_35.edmx", expectedResults[FrameworkVersion.V35], cancellationToken);
            }
            finally
            {
                await TestServices.SolutionExplorer.CloseSolutionAsync(cancellationToken);
            }
        }

        private async Task TestLoadingArtifactAsync(EnvDTE.Project project, string filePath, ArtifactStatus expectedArtifactStatus, CancellationToken cancellationToken)
        {
            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);

            Assert.NotNull(expectedArtifactStatus);

            // get a new artifact, which will automatically open up the EDMX file in VS
            EntityDesignArtifact entityDesignArtifact = null;

            try
            {
                var edmxProjectItem = project.GetProjectItemByName(Path.GetFileName(filePath));
                Assert.NotNull(edmxProjectItem);
                dte.OpenFile(edmxProjectItem.FileNames[0]);

                entityDesignArtifact =
                    (EntityDesignArtifact)_efArtifactHelper.GetNewOrExistingArtifact(
                        TestUtils.FileName2Uri(edmxProjectItem.FileNames[0]));

                Assert.Equal(expectedArtifactStatus.IsStructurallySafe, entityDesignArtifact.IsStructurallySafe);
                Assert.Equal(expectedArtifactStatus.IsVersionSafe, entityDesignArtifact.IsVersionSafe);
                Assert.Equal(expectedArtifactStatus.IsDesignerSafe, entityDesignArtifact.IsDesignerSafe);
            }
            finally
            {
                if (entityDesignArtifact != null)
                {
                    dte.CloseDocument(entityDesignArtifact.Uri.LocalPath, false);
                }
            }
        }

        /// <summary>
        ///     Determines whether a test should be skipped or not. We skip the test if:
        ///     - the skip-test registry key exists.
        ///     - the required .net framework is not installed in the target machine.
        /// </summary>
        /// <param name="requiredFrameworkVersion"></param>
        /// <returns></returns>
        private bool NETFrameworkVersionInstalled(FrameworkVersion requiredFrameworkVersion)
        {
            return Registry.LocalMachine.OpenSubKey(_mapFameworkRegistryPath[requiredFrameworkVersion]) != null;
        }
    }
}
