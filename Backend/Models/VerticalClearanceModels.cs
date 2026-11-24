using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;

namespace NavPro.Backend.Models
{
    public sealed class VerticalClearanceFinding
    {
        public string Pipe { get; set; }
        public VerticalCoverResult Result { get; set; }
        internal ModelItem Item { get; set; }
    }
}
