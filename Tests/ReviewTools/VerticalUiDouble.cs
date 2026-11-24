using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Models;
namespace NavPro.Backend.Services
{
    // Layout-only adapter. The actual extraction service is compiled against real SDK assemblies.
    public sealed class VerticalClearanceService
    {
        public static bool GeometrySupported => true;
        public VerticalClearanceService(ClashReviewSession session) { }
        public List<ModelItem> CurrentSelection()=>new List<ModelItem>();
        public List<ClashSetChoice> ReadChoices(string mode)=>new List<ClashSetChoice>();
        public List<ModelItem> FromChoice(ClashSetChoice choice)=>new List<ModelItem>();
        public List<ModelItem> FromSet(SelectionSet set)=>new List<ModelItem>();
        public void Show(VerticalClearanceFinding finding) { }
        public List<VerticalClearanceFinding> Run(IList<ModelItem> a,IList<ModelItem> b,double required,double tolerance,Action<string> progress)
            =>throw new NotSupportedException("No native geometry in UI fixture.");
    }
}
