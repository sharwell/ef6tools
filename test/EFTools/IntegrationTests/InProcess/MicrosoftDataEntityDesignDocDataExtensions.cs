namespace EFDesigner.IntegrationTests.InProcess
{
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Data.Entity.Design.EntityDesigner.View;
    using Microsoft.Data.Entity.Design.Package;

    internal static class MicrosoftDataEntityDesignDocDataExtensions
    {
        internal static EntityDesignerDiagram GetEntityDesignerDiagram(this MicrosoftDataEntityDesignDocData docData)
        {
            Debug.Assert(docData != null, "DocData not found");

            return (EntityDesignerDiagram)docData.DocViews
                                              .OfType<MicrosoftDataEntityDesignDocView>()
                                              .Single(view => view.Diagram is EntityDesignerDiagram)
                                              .Diagram;
        }

        internal static EntityDesignerDiagram GetEntityDesignerDiagram(this MicrosoftDataEntityDesignDocData docData, string diagramId)
        {
            Debug.Assert(docData != null, "DocData not found");
            Debug.Assert(!string.IsNullOrEmpty(diagramId), "!string.IsNullOrEmpty(diagramId)");

            return (EntityDesignerDiagram)docData.DocViews
                                              .OfType<MicrosoftDataEntityDesignDocView>()
                                              .Single(view => view.Diagram.DiagramId == diagramId)
                                              .Diagram;
        }

        internal static void OpenDiagram(this MicrosoftDataEntityDesignDocData docData, string diagramId)
        {
            Debug.Assert(docData != null, "DocData not found");
            Debug.Assert(!string.IsNullOrEmpty(diagramId), "!string.IsNullOrEmpty(diagramId)");

            docData.OpenDiagram(diagramId, true);
        }
    }
}
