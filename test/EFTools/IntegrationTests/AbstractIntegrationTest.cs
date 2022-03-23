namespace EFDesigner.IntegrationTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft;
    using Microsoft.Data.Entity.Design.VisualStudio.Package;
    using Microsoft.VisualStudio.Extensibility.Testing;
    using Microsoft.VisualStudio.Shell;
    using Xunit;

    [IdeSettings(MinVersion = VisualStudioVersion.VS2022, MaxVersion = VisualStudioVersion.VS2022, MaxAttempts = 2)]
    public abstract class AbstractIntegrationTest : AbstractIdeIntegrationTest
    {
        private IEdmPackage _package;

        public AbstractIntegrationTest()
        {
            TestContext = new TestContextImpl();
        }

        protected TestContextImpl TestContext { get; }

        private protected IEdmPackage Package
        {
            get
            {
                Assumes.Present(_package);
                return _package;
            }
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            if (await TestServices.SolutionExplorer.IsSolutionOpenAsync(HangMitigatingCancellationToken))
            {
                await TestServices.SolutionExplorer.CloseSolutionAsync(HangMitigatingCancellationToken);
            }

            await TestServices.StateReset.ResetGlobalOptionsAsync(HangMitigatingCancellationToken);
            await TestServices.StateReset.ResetHostSettingsAsync(HangMitigatingCancellationToken);

            await JoinableTaskFactory.SwitchToMainThreadAsync();
            PackageManager.LoadEDMPackage(ServiceProvider.GlobalProvider);
            _package = PackageManager.Package;
        }

        protected sealed class TestContextImpl
        {
            public string DeploymentDirectory
            {
                get
                {
                    var result = Path.GetDirectoryName(typeof(AbstractIntegrationTest).Assembly.Location);
                    if (!File.Exists(Path.Combine(result, "TestData", "AssocBetSubtypesV2SQLCE40.ssdl")))
                        throw new InvalidOperationException();

                    return result;
                }
            }

            public string TestRunDirectory => throw new NotImplementedException();

            public string TestName => throw new NotSupportedException();

            public void WriteLine(string text)
                => throw new NotImplementedException();
        }
    }
}
