using System;
using System.Collections.Generic;

[Serializable]
public class DungeonLayout
{
	public int Version { get; set; } = 1;
	public float CellSizeX { get; set; }
	public float CellSizeY { get; set; }
	public float CellSizeZ { get; set; }
	public int GridSizeX { get; set; }
	public int GridSizeY { get; set; }
	public int FloorHeightCells { get; set; } = 1;
	public List<FloorLayout> Floors { get; set; } = new();
}

[Serializable]
public class FloorLayout
{
	public int FloorIndex { get; set; }
	public List<CellData> FloorCells { get; set; } = new();
	public List<CellData> WallCells { get; set; } = new();
}

[Serializable]
public class CellData
{
	public int X { get; set; }
	public int Y { get; set; }
	public int Z { get; set; }
	public int TileId { get; set; }
	public int Orientation { get; set; }
}
