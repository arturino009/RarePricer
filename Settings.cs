using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace RarePricer
{
    public class Settings : ISettings
    {
        public Settings()
        {
            BorderWidth = new RangeNode<int>(5, 1, 15);
            BorderColor = new ColorNode(Color.Green);
            LinkCount = new RangeNode<int>(1, 1, 6);
        }

        public ToggleNode Enable { get; set; } = new ToggleNode(true);
        public RangeNode<int> BorderWidth { get; set; }
        public ColorNode BorderColor { get; set; }
        public RangeNode<int> LinkCount { get; set; }
    }
}