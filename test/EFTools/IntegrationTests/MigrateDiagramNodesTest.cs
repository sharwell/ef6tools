// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace EFDesigner.IntegrationTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using EFDesigner.IntegrationTests.InProcess;
    using Microsoft.Data.Entity.Design.Model;
    using Microsoft.Data.Entity.Design.Model.Commands;
    using Microsoft.Data.Entity.Design.Model.Designer;
    using Microsoft.Data.Entity.Design.Model.Entity;
    using Microsoft.Data.Entity.Design.Model.Eventing;
    using Microsoft.Data.Entity.Design.VisualStudio.Model.Commands;
    using Microsoft.Data.Entity.Design.VisualStudio.Package;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Xunit;

    /// <summary>
    ///     The purpose of the tests are:
    ///     - MigrateDiagramInformationCommand works as expected.
    ///     - To ensure that basic designer functionalities are still working after diagrams nodes are moved to separate file.
    /// </summary>
    public class MigrateDiagramNodesTest : AbstractIntegrationTest
    {
        private IEdmPackage _package;
        private readonly (string DeploymentDirectory, string TestRunDirectory) TestContext = (Path.GetDirectoryName(typeof(AutomaticDbContextTests).Assembly.Location), Path.GetDirectoryName(typeof(AutomaticDbContextTests).Assembly.Location));

        public override async Task InitializeAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            await base.InitializeAsync();

            PackageManager.LoadEDMPackage(ServiceProvider.GlobalProvider);
            _package = PackageManager.Package;
        }

        [IdeFact]
        public async Task SimpleAddEntity()
        {
            await ExecuteMigrateDiagramNodesTestAsync(
                "SimpleAddEntity",
                (artifact, commandProcessorContext) =>
                    {
                        var cet =
                            CreateEntityTypeCommand.CreateConceptualEntityTypeAndEntitySetAndProperty(
                                commandProcessorContext,
                                "entity1",
                                "entity1set",
                                true,
                                "id",
                                "String",
                                ModelConstants.StoreGeneratedPattern_Identity,
                                true);
                        Assert.True(cet != null, "EntityType is not created");

                        // Verify that EntityTypeShape is created in diagram1.
                        Assert.NotNull(
                            artifact.DiagramArtifact.Diagrams.FirstDiagram.EntityTypeShapes.SingleOrDefault(
                                ets => ets.EntityType.Target == cet));
                    },
                HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task SimpleDeleteEntity()
        {
            await ExecuteMigrateDiagramNodesTestAsync(
                "SimpleDeleteEntity",
                (artifact, commandProcessorContext) =>
                    {
                        var entity = artifact.ConceptualModel.EntityTypes().Single(et => et.Name.Value == "employee");
                        Assert.True(entity is not null, "Could not find Employee entity type.");

                        CommandProcessor.InvokeSingleCommand(commandProcessorContext, entity.GetDeleteCommand());
                        Assert.True(entity.IsDisposed);
                        Assert.True(
                            !artifact.DiagramArtifact.Diagrams.FirstDiagram.EntityTypeShapes.Any(
                                ets => ets.EntityType.Target.LocalName == entity.LocalName));
                    },
                HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task SimpleUndoRedo()
        {
            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(HangMitigatingCancellationToken);

            await ExecuteMigrateDiagramNodesTestAsync(
                "SimpleUndoRedo",
                (artifact, commandProcessorContext) =>
                    {
                        var baseType =
                            (ConceptualEntityType)
                            CreateEntityTypeCommand.CreateEntityTypeAndEntitySetWithDefaultNames(commandProcessorContext);
                        var derivedType = CreateEntityTypeCommand.CreateDerivedEntityType(
                            commandProcessorContext, "SubType", baseType, true);

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, "Edit.Undo");
                        Assert.True(!artifact.ConceptualModel.EntityTypes().Any(et => et.LocalName.Value == derivedType.LocalName.Value));

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, "Edit.Undo");
                        Assert.True(!artifact.ConceptualModel.EntityTypes().Any(et => et.LocalName.Value == baseType.LocalName.Value));

                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, "Edit.Redo");
                        dte.ExecuteCommandForOpenDocument(artifact.Uri.LocalPath, "Edit.Redo");

                        Assert.NotNull(
                            artifact.ConceptualModel.EntityTypes().SingleOrDefault(et => et.LocalName.Value == baseType.LocalName.Value));

                        // Verify that derived type and inheritance are recreated.
                        derivedType =
                            (ConceptualEntityType)
                            artifact.ConceptualModel.EntityTypes().SingleOrDefault(et => et.LocalName.Value == derivedType.LocalName.Value);
                        Assert.NotNull(derivedType);
                        Assert.Equal(baseType.LocalName.Value, derivedType.BaseType.Target.LocalName.Value);
                    },
                HangMitigatingCancellationToken);
        }

        private async Task ExecuteMigrateDiagramNodesTestAsync(string projectName, Action<EntityDesignArtifact, CommandProcessorContext> runTest, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(HangMitigatingCancellationToken);

            var modelPath = Path.Combine(TestContext.DeploymentDirectory, @"TestData\Model\v3\PubSimple.edmx");

            EntityDesignArtifact entityDesignArtifact = null;
            EnvDTE.Project project = null;

            try
            {
                project = dte.CreateProject(
                    TestContext.TestRunDirectory,
                    projectName,
                    DteExtensions.ProjectKind.Executable,
                    DteExtensions.ProjectLanguage.CSharp);

                var projectItem = dte.AddExistingItem(modelPath, project);
                dte.OpenFile(projectItem.FileNames[0]);

                entityDesignArtifact =
                    (EntityDesignArtifact)new EFArtifactHelper(EFArtifactHelper.GetEntityDesignModelManager(ServiceProvider.GlobalProvider))
                                                .GetNewOrExistingArtifact(TestUtils.FileName2Uri(projectItem.FileNames[0]));

                Debug.Assert(entityDesignArtifact != null);
                var editingContext =
                    _package.DocumentFrameMgr.EditingContextManager.GetNewOrExistingContext(entityDesignArtifact.Uri);

                // Create TransactionContext to indicate that the transactions are done from first diagram.
                // This is not used by MigrateDiagramInformationCommand but other commands in the callback methods.
                var transactionContext = new EfiTransactionContext();
                transactionContext.Add(
                    EfiTransactionOriginator.TransactionOriginatorDiagramId,
                    new DiagramContextItem(entityDesignArtifact.DesignerInfo.Diagrams.FirstDiagram.Id.Value));

                var commandProcessorContext =
                    new CommandProcessorContext(
                        editingContext, "MigrateDiagramNodesTest", projectName + "Txn", entityDesignArtifact, transactionContext);
                MigrateDiagramInformationCommand.DoMigrate(commandProcessorContext, entityDesignArtifact);

                Debug.Assert(entityDesignArtifact.DiagramArtifact != null);
                Debug.Assert(
                    entityDesignArtifact.IsDesignerSafe,
                    "Artifact should not be in safe mode after MigrateDiagramInformationCommand is executed.");
                Debug.Assert(
                    new Uri(entityDesignArtifact.Uri.LocalPath + EntityDesignArtifact.ExtensionDiagram)
                    == entityDesignArtifact.DiagramArtifact.Uri);

                runTest(entityDesignArtifact, commandProcessorContext);
            }
            finally
            {
                if (entityDesignArtifact != null)
                {
                    entityDesignArtifact.Dispose();
                }

                if (project != null)
                {
                    await TestServices.SolutionExplorer.CloseSolutionAsync(cancellationToken);
                }
            }
        }
    }
}
