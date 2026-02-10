using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace Delve
{
    public class DelveSettings : ISettings
    {
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        // Delve Pathways
        public ToggleNode DelvePathWays { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelvePathWaysNodeSize { get; set; } = new RangeNode<int>(7, 1, 200);
        public ColorNode DelvePathWaysNodeColor { get; set; } = new ColorNode(new Color(255, 140, 0, 255));

        public ToggleNode DelveWall { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelveWallSize { get; set; } = new RangeNode<int>(18, 1, 200);
        public ColorNode DelveWallColor { get; set; } = new ColorNode(Color.White);

        // Delve Chests
        public ToggleNode DelveChests { get; set; } = new ToggleNode(true);

        public ToggleNode DelveMiningSuppliesDynamiteChest { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelveMiningSuppliesDynamiteChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveMiningSuppliesDynamiteChestColor { get; set; } = new ColorNode(Color.White);

        public ToggleNode DelveMiningSuppliesFlaresChest { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelveMiningSuppliesFlaresChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveMiningSuppliesFlaresChestColor { get; set; } = new ColorNode(Color.White);

        public ToggleNode DelveAzuriteVeinChest { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelveAzuriteVeinChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveAzuriteVeinChestColor { get; set; } = new ColorNode(new Color(0, 115, 255, 255));

        public ToggleNode DelveCurrencyChest { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelveCurrencyChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveCurrencyChestColor { get; set; } = new ColorNode(Color.White);

        public ToggleNode DelveResonatorChest { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelveResonatorChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveResonatorChestColor { get; set; } = new ColorNode(Color.White);

        public ToggleNode DelveFossilChest { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelveFossilChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelveFossilChestColor { get; set; } = new ColorNode(Color.White);

        // Catch all remaining Delve chests
        public ToggleNode DelvePathwayChest { get; set; } = new ToggleNode(true);
        public RangeNode<int> DelvePathwayChestSize { get; set; } = new RangeNode<int>(15, 1, 200);
        public ColorNode DelvePathwayChestColor { get; set; } = new ColorNode(new Color(0, 131, 0, 255));

        // Delve Mine Map Connections
        public ToggleNode DelveMineMapConnections { get; set; } = new ToggleNode(true);
        public RangeNode<int> ShowRadiusPercentage { get; set; } = new RangeNode<int>(80, 0, 100);

        public ToggleNode DebugMode { get; set; } = new ToggleNode(false);
        public ToggleNode ShouldHideOnOpen { get; set; } = new ToggleNode(false);
        public HotkeyNodeV2 DebugHotkey { get; set; } = new HotkeyNodeV2(System.Windows.Forms.Keys.Menu);
    }
}
