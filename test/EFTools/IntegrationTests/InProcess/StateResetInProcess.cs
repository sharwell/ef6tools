namespace EFDesigner.IntegrationTests.InProcess
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Extensibility.Testing;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.Text.Editor;

    [TestService]
    internal partial class StateResetInProcess
    {
        /// <summary>
        /// Contains the persistence slots of tool windows to close between tests.
        /// </summary>
        /// <seealso cref="__VSFPROPID.VSFPROPID_GuidPersistenceSlot"/>
        private static readonly ImmutableHashSet<Guid> s_windowsToClose = ImmutableHashSet.Create(
            new Guid(EnvDTE.Constants.vsWindowKindObjectBrowser));

        public Task ResetGlobalOptionsAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task ResetHostSettingsAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Make sure responsive completion doesn't interfere if integration tests run slowly.
            var editorOptionsFactory = await GetComponentModelServiceAsync<IEditorOptionsFactoryService>(cancellationToken);
            var options = editorOptionsFactory.GlobalOptions;
            options.SetOptionValue(DefaultOptions.ResponsiveCompletionOptionId, false);

            var latencyGuardOptionKey = new EditorOptionKey<bool>("EnableTypingLatencyGuard");
            options.SetOptionValue(latencyGuardOptionKey, false);

#if false // Requires dependencies that are not yet generalized
            // Close any modal windows
            var mainWindow = await TestServices.Shell.GetMainWindowAsync(cancellationToken);
            var modalWindow = IntegrationHelper.GetModalWindowFromParentWindow(mainWindow);
            while (modalWindow != IntPtr.Zero)
            {
                if ("Default IME" == IntegrationHelper.GetTitleForWindow(modalWindow))
                {
                    // "Default IME" shows up as a modal window in some cases where there is no other window blocking
                    // input to Visual Studio.
                    break;
                }

                await TestServices.Input.SendWithoutActivateAsync(VirtualKey.Escape);
                var nextModalWindow = IntegrationHelper.GetModalWindowFromParentWindow(mainWindow);
                if (nextModalWindow == modalWindow)
                {
                    // Don't loop forever if windows aren't closing.
                    break;
                }
            }
#endif

            // Close tool windows where desired (see s_windowsToClose)
            await foreach (var window in TestServices.Shell.EnumerateWindowsAsync(__WindowFrameTypeFlags.WINDOWFRAMETYPE_Tool, cancellationToken).WithCancellation(cancellationToken))
            {
                ErrorHandler.ThrowOnFailure(window.GetGuidProperty((int)__VSFPROPID.VSFPROPID_GuidPersistenceSlot, out var persistenceSlot));
                if (s_windowsToClose.Contains(persistenceSlot))
                {
                    window.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                }
            }
        }
    }
}
