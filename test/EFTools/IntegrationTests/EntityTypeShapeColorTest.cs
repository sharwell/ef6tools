namespace EFDesigner.IntegrationTests
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using EFDesigner.IntegrationTests.InProcess;
    using Microsoft.Data.Entity.Design.Base.Context;
    using Microsoft.Data.Entity.Design.Model;
    using Microsoft.Data.Entity.Design.Model.Commands;
    using Microsoft.Data.Entity.Design.Model.Entity;
    using Microsoft.Data.Entity.Design.Package;
    using Microsoft.Data.Entity.Design.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Xunit;
    using EntityDesignerView = Microsoft.Data.Entity.Design.EntityDesigner.View;
    using EntityDesignerViewModel = Microsoft.Data.Entity.Design.EntityDesigner.ViewModel;

    public class EntityTypeShapeColorTest : AbstractIntegrationTest
    {
        [IdeFact]
        public async Task ChangeEntityTypeShapeFillColorTest()
        {
            var shapeColor = Color.Beige;
            await ChangeEntityTypesFillColorTestAsync(
                "ChangeShapeColor",
                shapeColor,
                async (dte, artifact, commandProcessor, cancellationToken) =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    var entityDesignerDiagram = GetDocData(commandProcessor.EditingContext).GetEntityDesignerDiagram();
                    Assert.True(entityDesignerDiagram is not null, "Could not get an instance of EntityDesignerDiagram from editingcontext.");

                    foreach (var ets in entityDesignerDiagram.NestedChildShapes.OfType<EntityDesignerView.EntityTypeShape>())
                    {
                        var entityType = (EntityDesignerViewModel.EntityType)ets.ModelElement;
                        Assert.NotNull(entityType);
                        Assert.Equal(shapeColor, ets.FillColor);
                    }
                },
                HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task CopyAndPasteInSingleDiagramTest()
        {
            var shapeColor = Color.Red;
            await ChangeEntityTypesFillColorTestAsync(
                "CopyAndPasteSingle",
                shapeColor,
                async (dte, artifact, commandProcessor, cancellationToken) =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    var entityDesignerDiagram = GetDocData(commandProcessor.EditingContext).GetEntityDesignerDiagram();
                    Assert.True(entityDesignerDiagram is not null, "Could not get an instance of EntityDesignerDiagram from editingcontext.");

                    var author = entityDesignerDiagram.GetShape("author");
                    Assert.True(author is not null, "Could not get DSL entity type shape instance of 'author'.");

                    var titleAuthor = entityDesignerDiagram.GetShape("titleauthor");
                    Assert.True(titleAuthor is not null, "Could not get DSL entity type shape instance of 'titleauthor'.");

                    entityDesignerDiagram.SelectDiagramItems(new[] { author, titleAuthor });

                    DesignerUtilities.Copy(dte);
                    DesignerUtilities.Paste(dte);

                    var authorCopy = entityDesignerDiagram.GetShape("author1");
                    Assert.True(authorCopy is not null, "Entity: 'author1' should have been created.");
                    var authorCopyModel =
                        (EntityType)
                        authorCopy.TypedModelElement.EntityDesignerViewModel.ModelXRef.GetExisting(authorCopy.TypedModelElement);
                    Assert.True(
                        authorCopyModel.GetAntiDependenciesOfType<AssociationEnd>().FirstOrDefault() is not null,
                        "The association between author1 and titleauthor1 should have been created.");
                    Assert.Equal(shapeColor, authorCopy.FillColor);

                    var titleAuthorCopy = entityDesignerDiagram.GetShape("titleauthor1");
                    Assert.True(titleAuthorCopy is not null, "Entity: 'titleauthor1' should have been created.");
                    Assert.Equal(shapeColor, titleAuthorCopy.FillColor);
                },
                HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task CopyAndPasteInMultipleDiagramsTest()
        {
            var shapeColor = Color.Brown;
            await ChangeEntityTypesFillColorTestAsync(
                "CopyAndPasteMulti",
                shapeColor,
                async (dte, artifact, commandProcessorContext, cancellationToken) =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    var docData = GetDocData(commandProcessorContext.EditingContext);
                    var entityDesignerDiagram = docData.GetEntityDesignerDiagram();
                    Assert.True(entityDesignerDiagram is not null, "Could not get an instance of EntityDesignerDiagram from editingcontext.");

                    var author = entityDesignerDiagram.GetShape("author");
                    Assert.True(author is not null, "Could not get DSL entity type shape instance of 'author'.");

                    var titleAuthor = entityDesignerDiagram.GetShape("titleauthor");
                    Assert.True(titleAuthor is not null, "Could not get DSL entity type shape instance of 'titleauthor'.");

                    entityDesignerDiagram.SelectDiagramItems(new[] { author, titleAuthor });
                    DesignerUtilities.Copy(dte);

                    var diagram = CreateDiagramCommand.CreateDiagramWithDefaultName(commandProcessorContext);
                    docData.OpenDiagram(diagram.Id.Value);

                    DesignerUtilities.Paste(dte);

                    // Get the newly created diagram.
                    entityDesignerDiagram = docData.GetEntityDesignerDiagram(diagram.Id.Value);

                    author = entityDesignerDiagram.GetShape("author");
                    Assert.True(author is not null, "Entity: 'author' should exists in diagram:" + diagram.Name);

                    titleAuthor = entityDesignerDiagram.GetShape("titleauthor");
                    Assert.True(titleAuthor is not null, "Entity: 'titleauthor' should exists in diagram:" + diagram.Name);

                    var associationConnector =
                        entityDesignerDiagram.NestedChildShapes.OfType<EntityDesignerView.AssociationConnector>().FirstOrDefault();
                    Assert.True(
                        associationConnector is not null,
                        "There should have been association connector created between entities 'author' and 'titleauthor'.");

                    var entityDesignerViewModel = entityDesignerDiagram.ModelElement;
                    Assert.True(entityDesignerViewModel is not null, "Diagram's ModelElement is not a type of EntityDesignerViewModel");

                    var association = (Association)entityDesignerViewModel.ModelXRef.GetExisting(associationConnector.ModelElement);
                    Assert.True(
                        association is not null,
                        "Could not find association for associationConnector" + associationConnector.AccessibleName
                        + " from Model Xref.");

                    var entityTypesInAssociation = association.AssociationEnds().Select(ae => ae.Type.Target).Distinct().ToList();
                    Assert.Equal(2, entityTypesInAssociation.Count);
                    Assert.False(
                        entityTypesInAssociation.Any(et => et.LocalName.Value != "author" && et.LocalName.Value != "titleauthor"),
                        "The association between author and title-author is not created in diagram: " + diagram.Name);

                    Assert.Equal(shapeColor, author.FillColor);
                    Assert.Equal(shapeColor, titleAuthor.FillColor);
                },
                HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task AddRelatedItemsTest()
        {
            var shapeColor = Color.Cyan;
            await ChangeEntityTypesFillColorTestAsync(
                "AddRelatedItems",
                shapeColor,
                async (dte, artifact, commandProcessorContext, cancellationToken) =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                    var diagram = CreateDiagramCommand.CreateDiagramWithDefaultName(commandProcessorContext);
                    var docData = GetDocData(commandProcessorContext.EditingContext);
                    docData.OpenDiagram(diagram.Id.Value);

                    CreateEntityTypeShapeCommand.CreateEntityTypeShapeAndConnectorsInDiagram(
                        commandProcessorContext,
                        diagram,
                        (ConceptualEntityType)artifact.ConceptualModel.EntityTypes().Single(et => et.LocalName.Value == "author"),
                        shapeColor, false);

                    var entityDesignerDiagram = docData.GetEntityDesignerDiagram(diagram.Id.Value);
                    var author = entityDesignerDiagram.GetShape("author");
                    Assert.True(author is not null, "Could not get DSL entity type shape instance of 'author'.");

                    entityDesignerDiagram.SelectDiagramItems(new[] { author });
                    dte.ExecuteCommand("OtherContextMenus.MicrosoftDataEntityDesignContext.IncludeRelated");

                    var titleauthor = entityDesignerDiagram.GetShape("titleauthor");
                    Assert.True(titleauthor is not null, "Could not get DSL entity type shape instance of 'titleauthor'.");

                    Assert.Equal(shapeColor, author.FillColor);
                    Assert.Equal(shapeColor, titleauthor.FillColor);
                },
                HangMitigatingCancellationToken);
        }

        private async Task ChangeEntityTypesFillColorTestAsync(
            string projectName, Color fillColor, Func<EnvDTE.DTE, EntityDesignArtifact, CommandProcessorContext, CancellationToken, Task> runTestAsync, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);

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

                var editingContext =
                    Package.DocumentFrameMgr.EditingContextManager.GetNewOrExistingContext(entityDesignArtifact.Uri);
                var commandProcessorContext = new CommandProcessorContext(
                    editingContext, "DiagramTest", "DiagramTestTxn", entityDesignArtifact);

                foreach (var ets in entityDesignArtifact.DesignerInfo.Diagrams.FirstDiagram.EntityTypeShapes)
                {
                    CommandProcessor.InvokeSingleCommand(
                        commandProcessorContext, new UpdateDefaultableValueCommand<Color>(ets.FillColor, fillColor));
                }

                await runTestAsync(dte, entityDesignArtifact, commandProcessorContext, cancellationToken);
            }
            finally
            {
                if (entityDesignArtifact != null)
                {
                    entityDesignArtifact.Dispose();
                }

                if (project != null)
                {
                    dte.CloseSolution(false);
                }
            }
        }

        private static MicrosoftDataEntityDesignDocData GetDocData(EditingContext editingContext)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Debug.Assert(editingContext != null, "editingContext != null");

            var artifactService = editingContext.GetEFArtifactService();
            return (MicrosoftDataEntityDesignDocData)VSHelpers.GetDocData(ServiceProvider.GlobalProvider, artifactService.Artifact.Uri.LocalPath);
        }
    }
}
