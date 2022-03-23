// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace EFDesigner.IntegrationTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using EFDesigner.IntegrationTests.InProcess;
    using Microsoft.Data.Entity.Design.Model;
    using Microsoft.Data.Entity.Design.Model.Commands;
    using Microsoft.Data.Entity.Design.Refactoring;
    using Microsoft.Data.Entity.Design.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Xunit;

    public class RefactorRenameTests : AbstractIntegrationTest
    {
        private const string PubSimpleProgramText = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace {0}
{{
    class Program
    {{
        static void Main(string[] args)
        {{
            using (var c = new PUBSEntities())
            {{
                author author = new author() {{ au_id = ""foo"" }};
                c.AddToauthors(author);

                foreach (var i in c.authors)
                {{}}
            }}
        }}
    }}
}}";

        private const string RefactorRenameEntityResult = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RefactorRenameEntity
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var c = new PUBSEntities())
            {
                renamedAuthor author = new renamedAuthor() { au_id = ""foo"" };
                c.AddTorenamedAuthor(author);

                foreach (var i in c.renamedAuthor)
                {}
            }
        }
    }
}";

        private const string RefactorRenamePropertyResult = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RefactorRenameProperty
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var c = new PUBSEntities())
            {
                author author = new author() { renamedId = ""foo"" };
                c.AddToauthors(author);

                foreach (var i in c.authors)
                {}
            }
        }
    }
}";

        [IdeFact(Skip = "http://entityframework.codeplex.com/workitem/992")]
        public async Task RefactorRenameEntity()
        {
            await RefactorRenameTestAsync(
                "RefactorRenameEntity", (artifact, cpc, programDocData) =>
                    {
                        var authorType = ModelHelper.FindEntityType(artifact.ConceptualModel, "author");
                        Assert.True(authorType is not null, "Could not find author type in the model");

                        RefactorEFObject.RefactorRenameElement(authorType, "renamedAuthor", false);

                        var textLines = VSHelpers.GetVsTextLinesFromDocData(programDocData);
                        Assert.True(textLines is not null, "Could not get VsTextLines for program DocData");

                        Assert.Equal(
                            RefactorRenameEntityResult, VSHelpers.GetTextFromVsTextLines(textLines));

                        var renamedAuthorType = ModelHelper.FindEntityType(artifact.ConceptualModel, "renamedAuthor");
                        Assert.True(renamedAuthorType is not null, "Could not find renamedAuthor type in the model");
                    },
                HangMitigatingCancellationToken);
        }

        [IdeFact(Skip = "http://entityframework.codeplex.com/workitem/992")]
        public async Task RefactorRenameProperty()
        {
            await RefactorRenameTestAsync(
                "RefactorRenameProperty", (artifact, cpc, programDocData) =>
                    {
                        var authorType = ModelHelper.FindEntityType(artifact.ConceptualModel, "author");
                        Assert.True(authorType is not null, "Could not find author type in the model");

                        var idProperty = ModelHelper.FindProperty(authorType, "au_id");
                        Assert.True(idProperty is not null, "Could not find au_id property in the model");

                        RefactorEFObject.RefactorRenameElement(idProperty, "renamedId", false);

                        var textLines = VSHelpers.GetVsTextLinesFromDocData(programDocData);
                        Assert.True(textLines is not null, "Could not get VsTextLines for program DocData");

                        Assert.Equal(
                            RefactorRenamePropertyResult, VSHelpers.GetTextFromVsTextLines(textLines));

                        authorType = ModelHelper.FindEntityType(artifact.ConceptualModel, "author");
                        Assert.True(authorType is not null, "Could not find author type in the model");

                        var renamedIdProperty = ModelHelper.FindProperty(authorType, "renamedId");
                        Assert.True(renamedIdProperty is not null, "Could not find renamedId property in the model");
                    },
                HangMitigatingCancellationToken);
        }

        private async Task RefactorRenameTestAsync(string projectName, Action<EntityDesignArtifact, CommandProcessorContext, object> test, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var modelEdmxFilePath = Path.Combine(TestContext.DeploymentDirectory, @"TestData\Model\v3\PubSimple.edmx");
            var dte = await TestServices.Shell.GetRequiredGlobalServiceAsync<SDTE, EnvDTE.DTE>(cancellationToken);
            var serviceProvider = ServiceProvider.GlobalProvider;

            EntityDesignArtifact entityDesignArtifact = null;
            try
            {
                var project = dte.CreateProject(
                    TestContext.TestRunDirectory,
                    projectName,
                    DteExtensions.ProjectKind.Executable,
                    DteExtensions.ProjectLanguage.CSharp);

                var projectItem = dte.AddExistingItem(new FileInfo(modelEdmxFilePath).FullName, project);
                dte.OpenFile(projectItem.FileNames[0]);
                entityDesignArtifact =
                    (EntityDesignArtifact)new EFArtifactHelper(EFArtifactHelper.GetEntityDesignModelManager(serviceProvider))
                                                .GetNewOrExistingArtifact(TestUtils.FileName2Uri(projectItem.FileNames[0]));

                var editingContext =
                    Package.DocumentFrameMgr.EditingContextManager.GetNewOrExistingContext(entityDesignArtifact.Uri);
                var cpc = new CommandProcessorContext(
                    editingContext, "DiagramTest" + projectName, "DiagramTestTxn" + projectName, entityDesignArtifact);

                var programDocData = VSHelpers.GetDocData(
                    serviceProvider, Path.Combine(Path.GetDirectoryName(project.FullName), "Program.cs"));
                Debug.Assert(programDocData != null, "Could not get DocData for program file");

                var textLines = VSHelpers.GetVsTextLinesFromDocData(programDocData);
                Debug.Assert(textLines != null, "Could not get VsTextLines for program DocData");

                VsUtils.SetTextForVsTextLines(textLines, string.Format(PubSimpleProgramText, projectName));
                test(entityDesignArtifact, cpc, programDocData);
            }
            finally
            {
                if (entityDesignArtifact != null)
                {
                    entityDesignArtifact.Dispose();
                }

                await TestServices.SolutionExplorer.CloseSolutionAsync(cancellationToken);
            }
        }
    }
}
