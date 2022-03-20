namespace EFDesigner.IntegrationTests
{
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Extensibility.Testing;
    using Xunit;

    [IdeSettings(MinVersion = VisualStudioVersion.VS2022, MaxVersion = VisualStudioVersion.VS2022, MaxAttempts = 2)]
    public abstract class AbstractIntegrationTest : AbstractIdeIntegrationTest
    {
        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            if (await TestServices.SolutionExplorer.IsSolutionOpenAsync(HangMitigatingCancellationToken))
            {
                await TestServices.SolutionExplorer.CloseSolutionAsync(HangMitigatingCancellationToken);
            }

            await TestServices.StateReset.ResetGlobalOptionsAsync(HangMitigatingCancellationToken);
            await TestServices.StateReset.ResetHostSettingsAsync(HangMitigatingCancellationToken);
        }
    }
}
