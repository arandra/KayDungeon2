using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

[Tool]
public partial class DungeonGenerator : Node3D
{
	[Export] public string GltfDir = "res://Assets/Map/kaykit_dungeon/Assets/gltf";

	[Export] public Vector3 CellSize = new(4.0f, 4.0f, 4.0f);
	[Export] public bool RandomSeed = true;
	[Export] public int Seed = 0;

	[Export] public int RoomCount = 8;
	[Export] public Vector2I MinRoomSize = new(4, 4);
	[Export] public Vector2I MaxRoomSize = new(10, 10);
	[Export] public Vector2I GridSize = new(48, 48);
	[Export] public int CorridorWidth = 1;

	[Export] public string[] FloorSmallAssets = Array.Empty<string>();
	[Export] public string[] FloorLargeAssets =
	{
		"floor_tile_large.gltf",
		"floor_dirt_large.gltf"
	};
	[Export] public string[] FloorExtralargeAssets =
	{
		"floor_tile_extralarge_grates.gltf"
	};
	[Export] public string[] FloorWoodSmallAssets = Array.Empty<string>();
	[Export] public string[] FloorWoodLargeAssets =
	{
		"floor_wood_large.gltf",
		"floor_wood_large_dark.gltf"
	};
	[Export] public string[] FloorSmallVariantAssets = Array.Empty<string>();
	[Export] public string[] FloorLargeVariantAssets =
	{
		"floor_tile_large_rocks.gltf",
		"floor_dirt_large_rocky.gltf"
	};
	[Export] public string[] FloorTrapAssets =
	{
		"floor_tile_big_grate.gltf",
		"floor_tile_big_spikes.gltf"
	};

	[Export] public string WallAsset = "wall.gltf";
	[Export] public string WallCornerAsset = "wall_corner.gltf";
	[Export] public string WallEndcapAsset = "wall_endcap.gltf";
	[Export] public string[] WallVariantAssets =
	{
		"wall_cracked.gltf",
		"wall_broken.gltf"
	};
	[Export] public string WallTsplitAsset = "wall_Tsplit.gltf";
	[Export] public string WallCrossingAsset = "wall_crossing.gltf";
	[Export] public string[] WallDoorwayAssets =
	{
		"wall_doorway.gltf"
	};
	[Export] public string[] WallWindowAssets =
	{
		"wall_window_open.gltf",
		"wall_window_closed.gltf"
	};
	[Export] public string[] WallArchedAssets =
	{
		"wall_arched.gltf"
	};

	[Export] public bool RegenerateOnReady = true;
	[Export] public bool EnableWallDecor = false;
	[Export] public bool EnableProps = false;
	[Export] public bool DebugDumpEnabled = false;
	[Export] public bool DebugDumpOnGenerate = true;
	[Export] public bool DebugDumpGridCsv = true;
	[Export] public bool DebugDumpRoomsCsv = true;
	[Export] public bool DebugDumpWallCornerCsv = true;
	[Export] public int SpecialRoomCount = 1;
	[Export] public float WoodRoomChance = 0.15f;
	[Export] public float TrapChance = 0.05f;
	[Export] public float LargeVariantChance = 0.15f;
	[Export] public float SmallVariantChance = 0.12f;
	[Export] public float WallVariantChance = 0.08f;
	[Export] public float DoorChance = 0.08f;
	[Export] public float WindowChance = 0.05f;
	[Export] public float ArchedChance = 0.03f;
	[Export] public float WallRotationOffsetDegrees = 90.0f;

	[Export] public bool EditorGenerate = false;
	[Export] public bool EditorClear = false;

	private enum CellType
	{
		Empty = 0,
		Room = 1,
		Corridor = 2,
		Special = 3
	}

	private GridMap _gridMap;
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<int> _floorSmallIds = new();
	private readonly List<int> _floorLargeIds = new();
	private readonly List<int> _floorExtralargeIds = new();
	private readonly List<int> _floorWoodSmallIds = new();
	private readonly List<int> _floorWoodLargeIds = new();
	private readonly List<int> _floorSmallVariantIds = new();
	private readonly List<int> _floorLargeVariantIds = new();
	private readonly List<int> _floorTrapIds = new();
	private int _wallId = -1;
	private int _wallCornerId = -1;
	private int _wallEndcapId = -1;
	private int _wallTsplitId = -1;
	private int _wallCrossingId = -1;
	private readonly List<int> _wallVariantIds = new();
	private readonly List<int> _wallDoorwayIds = new();
	private readonly List<int> _wallWindowIds = new();
	private readonly List<int> _wallArchedIds = new();
	private int[,] _lastGrid;
	private List<Rect2I> _lastRooms = new();
	private HashSet<int> _lastSpecialRooms = new();
	private HashSet<int> _lastWoodRooms = new();
	private readonly List<string> _cornerSkips = new();

	public override void _Ready()
	{
		_gridMap = GetNodeOrNull<GridMap>("GridMap");
		if (_gridMap == null)
		{
			GD.Print("DungeonGenerator: GridMap not found in _Ready.");
			return;
		}

		if (RandomSeed)
		{
			_rng.Randomize();
		}
		else
		{
			_rng.Seed = (ulong)Seed;
		}

		_gridMap.CellSize = CellSize;
		_gridMap.MeshLibrary = BuildMeshLibrary();

			if (RegenerateOnReady && !Engine.IsEditorHint())
			{
				Generate();
			}
		}

		public override void _Process(double delta)
		{
			if (!Engine.IsEditorHint())
		{
			return;
		}

		if (EditorGenerate)
		{
			EditorGenerate = false;
			GD.Print("DungeonGenerator: EditorGenerate triggered.");
			EditorGenerateInternal();
			NotifyPropertyListChanged();
		}

		if (EditorClear)
		{
			EditorClear = false;
			EditorClearInternal();
			NotifyPropertyListChanged();
		}
	}

	public void Generate()
	{
		if (_gridMap?.MeshLibrary == null)
		{
			GD.PushWarning("MeshLibrary is missing. Cannot generate.");
			return;
		}

		_gridMap.Clear();

		var grid = MakeGrid(GridSize);
		var rooms = CarveRooms(grid);
		var specialRooms = PickRoomIndices(rooms, SpecialRoomCount);
		var woodRooms = PickRoomIndices(rooms, Mathf.CeilToInt(rooms.Count * WoodRoomChance));
		_lastGrid = grid;
		_lastRooms = rooms;
		_lastSpecialRooms = specialRooms;
		_lastWoodRooms = woodRooms;
		MarkSpecialRooms(grid, rooms, specialRooms);
		ConnectRooms(grid, rooms);
		PlaceFloors(grid, rooms, specialRooms, woodRooms);
		PlaceWalls(grid);
		if (DebugDumpEnabled && DebugDumpOnGenerate)
		{
			DumpDebugData();
		}
		if (EnableWallDecor)
		{
			PlaceWallDecor(grid, rooms);
		}
		if (EnableProps)
		{
			PlaceProps(grid, rooms);
		}
	}

	private void EditorGenerateInternal()
	{
		if (_gridMap == null)
		{
			_gridMap = GetNodeOrNull<GridMap>("GridMap");
		}
		if (_gridMap == null)
		{
			GD.Print("DungeonGenerator: GridMap missing. Cannot generate.");
			return;
		}

		_gridMap.CellSize = CellSize;
		_gridMap.MeshLibrary = BuildMeshLibrary();
		Generate();
	}

	private void EditorClearInternal()
	{
		_gridMap?.Clear();
	}

	private int[,] MakeGrid(Vector2I size)
	{
		var grid = new int[size.X, size.Y];
		for (var x = 0; x < size.X; x++)
		{
			for (var z = 0; z < size.Y; z++)
			{
				grid[x, z] = (int)CellType.Empty;
			}
		}
		return grid;
	}

	private List<Rect2I> CarveRooms(int[,] grid)
	{
		var rooms = new List<Rect2I>();
		var attempts = RoomCount * 6;
		for (var i = 0; i < attempts; i++)
		{
			if (rooms.Count >= RoomCount)
			{
				break;
			}

			var roomSize = new Vector2I(
				_rng.RandiRange(MinRoomSize.X, MaxRoomSize.X),
				_rng.RandiRange(MinRoomSize.Y, MaxRoomSize.Y)
			);
			var position = new Vector2I(
				_rng.RandiRange(1, GridSize.X - roomSize.X - 2),
				_rng.RandiRange(1, GridSize.Y - roomSize.Y - 2)
			);
			var rect = new Rect2I(position, roomSize);

			if (IntersectsAny(rect, rooms))
			{
				continue;
			}

			CarveRect(grid, rect);
			rooms.Add(rect);
		}

		return rooms;
	}

	private static bool IntersectsAny(Rect2I rect, List<Rect2I> rooms)
	{
		foreach (var other in rooms)
		{
			if (rect.Grow(1).Intersects(other))
			{
				return true;
			}
		}
		return false;
	}

	private static void CarveRect(int[,] grid, Rect2I rect)
	{
		for (var x = rect.Position.X; x < rect.Position.X + rect.Size.X; x++)
		{
			for (var z = rect.Position.Y; z < rect.Position.Y + rect.Size.Y; z++)
			{
				grid[x, z] = (int)CellType.Room;
			}
		}
	}

	private void ConnectRooms(int[,] grid, List<Rect2I> rooms)
	{
		if (rooms.Count <= 1)
		{
			return;
		}

		for (var i = 1; i < rooms.Count; i++)
		{
			var fromCenter = RectCenter(rooms[i - 1]);
			var toCenter = RectCenter(rooms[i]);
			CarveCorridor(grid, fromCenter, toCenter);
		}
	}

	private static Vector2I RectCenter(Rect2I rect)
	{
		return new Vector2I(
			rect.Position.X + rect.Size.X / 2,
			rect.Position.Y + rect.Size.Y / 2
		);
	}

	private void CarveCorridor(int[,] grid, Vector2I from, Vector2I to)
	{
		var horizontalFirst = _rng.RandiRange(0, 1) == 0;
		if (horizontalFirst)
		{
			CarveLine(grid, new Vector2I(from.X, from.Y), new Vector2I(to.X, from.Y));
			CarveLine(grid, new Vector2I(to.X, from.Y), new Vector2I(to.X, to.Y));
		}
		else
		{
			CarveLine(grid, new Vector2I(from.X, from.Y), new Vector2I(from.X, to.Y));
			CarveLine(grid, new Vector2I(from.X, to.Y), new Vector2I(to.X, to.Y));
		}
	}

	private void CarveLine(int[,] grid, Vector2I start, Vector2I end)
	{
		var dx = Math.Sign(end.X - start.X);
		var dz = Math.Sign(end.Y - start.Y);
		var x = start.X;
		var z = start.Y;
		if (grid[x, z] == (int)CellType.Empty)
		{
			grid[x, z] = (int)CellType.Corridor;
		}

		while (x != end.X || z != end.Y)
		{
			if (x != end.X)
			{
				x += dx;
			}
			else if (z != end.Y)
			{
				z += dz;
			}
			CarveCorridorCell(grid, new Vector2I(x, z));
		}
	}

	private void CarveCorridorCell(int[,] grid, Vector2I center)
	{
		for (var offsetX = -CorridorWidth + 1; offsetX < CorridorWidth; offsetX++)
		{
			for (var offsetZ = -CorridorWidth + 1; offsetZ < CorridorWidth; offsetZ++)
			{
				var x = center.X + offsetX;
				var z = center.Y + offsetZ;
				if (InBounds(new Vector2I(x, z)) && grid[x, z] == (int)CellType.Empty)
				{
					grid[x, z] = (int)CellType.Corridor;
				}
			}
		}
	}

	private void MarkSpecialRooms(int[,] grid, List<Rect2I> rooms, HashSet<int> specials)
	{
		foreach (var index in specials)
		{
			var room = rooms[index];
			for (var x = room.Position.X; x < room.Position.X + room.Size.X; x++)
			{
				for (var z = room.Position.Y; z < room.Position.Y + room.Size.Y; z++)
				{
					grid[x, z] = (int)CellType.Special;
				}
			}
		}
	}

	private void PlaceFloors(int[,] grid, List<Rect2I> rooms, HashSet<int> specials, HashSet<int> woodRooms)
	{
		var occupied = new bool[GridSize.X, GridSize.Y];
		var specialSize = 2;
		if (_floorExtralargeIds.Count > 0)
		{
			foreach (var index in specials)
			{
				var room = rooms[index];
				if (room.Size.X < specialSize || room.Size.Y < specialSize)
				{
					continue;
				}
				var start = new Vector2I(
					room.Position.X + (room.Size.X - specialSize) / 2,
					room.Position.Y + (room.Size.Y - specialSize) / 2
				);
				if (CanPlaceBlock(grid, occupied, start, specialSize))
				{
					var tileId = PickFrom(_floorExtralargeIds);
					PlaceBlockTile(occupied, start, specialSize, tileId);
				}
			}
		}

		var largeSize = 1;
		for (var r = 0; r < rooms.Count; r++)
		{
			var room = rooms[r];
			var useWood = woodRooms.Contains(r);
			for (var x = room.Position.X; x < room.Position.X + room.Size.X; x += largeSize)
			{
				for (var z = room.Position.Y; z < room.Position.Y + room.Size.Y; z += largeSize)
				{
					var start = new Vector2I(x, z);
					if (!CanPlaceBlock(grid, occupied, start, largeSize))
					{
						continue;
					}
					var tileId = PickLargeTile(useWood);
					if (_rng.Randf() < LargeVariantChance)
					{
						tileId = PickFrom(_floorLargeVariantIds, tileId);
					}
					PlaceBlockTile(occupied, start, largeSize, tileId);
				}
			}
		}

		for (var x = 0; x < GridSize.X; x++)
		{
			for (var z = 0; z < GridSize.Y; z++)
			{
				if (grid[x, z] == (int)CellType.Empty || occupied[x, z])
				{
					continue;
				}

				var tileId = PickLargeTile(false);
				if (_rng.Randf() < LargeVariantChance)
				{
					tileId = PickFrom(_floorLargeVariantIds, tileId);
				}
				if (_rng.Randf() < TrapChance && grid[x, z] != (int)CellType.Special)
				{
					tileId = PickFrom(_floorTrapIds, tileId);
				}
				_gridMap.SetCellItem(new Vector3I(x, 0, z), tileId);
				occupied[x, z] = true;
			}
		}
	}

	private void PlaceWalls(int[,] grid)
	{
		var walls = new Dictionary<Vector2I, Vector2I>();
		var occupied = new bool[GridSize.X, GridSize.Y];
		_cornerSkips.Clear();
		for (var x = 0; x < GridSize.X; x++)
		{
			for (var z = 0; z < GridSize.Y; z++)
			{
				if (grid[x, z] == (int)CellType.Empty)
				{
					continue;
				}

				foreach (var dir in CardinalDirs())
				{
					var neighbor = new Vector2I(x, z) + dir;
					if (!InBounds(neighbor) || grid[neighbor.X, neighbor.Y] == (int)CellType.Empty)
					{
						if (!walls.ContainsKey(neighbor))
						{
							walls[neighbor] = dir;
						}
					}
				}
			}
		}

		foreach (var entry in walls)
		{
			if (occupied[entry.Key.X, entry.Key.Y])
			{
				if (DebugDumpEnabled && DebugDumpWallCornerCsv)
				{
					RecordCornerSkip(entry.Key, entry.Value, grid, "occupied");
				}
				continue;
			}
			PlaceWallTile(entry.Key, entry.Value, grid);
			occupied[entry.Key.X, entry.Key.Y] = true;
		}
	}
		private void PlaceWallDecor(int[,] grid, List<Rect2I> rooms)
		{
		}

	private void PlaceProps(int[,] grid, List<Rect2I> rooms)
	{
	}

	private void DumpDebugData()
	{
		var debugDir = "res://Docs/debug";
		var debugPath = ProjectSettings.GlobalizePath(debugDir);
		Directory.CreateDirectory(debugPath);

		if (DebugDumpGridCsv && _lastGrid != null)
		{
			var gridBuilder = new StringBuilder();
			for (var z = 0; z < GridSize.Y; z++)
			{
				for (var x = 0; x < GridSize.X; x++)
				{
					if (x > 0)
					{
						gridBuilder.Append(',');
					}
					gridBuilder.Append(_lastGrid[x, z]);
				}
				gridBuilder.AppendLine();
			}
			File.WriteAllText(Path.Combine(debugPath, "dungeon_grid.csv"), gridBuilder.ToString(), Encoding.UTF8);
		}

		if (DebugDumpRoomsCsv && _lastRooms != null)
		{
			var roomsBuilder = new StringBuilder();
			roomsBuilder.AppendLine("index,x,y,w,h,is_special,is_wood");
			for (var i = 0; i < _lastRooms.Count; i++)
			{
				var room = _lastRooms[i];
				var isSpecial = _lastSpecialRooms.Contains(i) ? 1 : 0;
				var isWood = _lastWoodRooms.Contains(i) ? 1 : 0;
				roomsBuilder.AppendLine($"{i},{room.Position.X},{room.Position.Y},{room.Size.X},{room.Size.Y},{isSpecial},{isWood}");
			}
			File.WriteAllText(Path.Combine(debugPath, "dungeon_rooms.csv"), roomsBuilder.ToString(), Encoding.UTF8);
		}

		if (DebugDumpWallCornerCsv && _cornerSkips.Count > 0)
		{
			var cornersBuilder = new StringBuilder();
			cornersBuilder.AppendLine("x,z,dir_x,dir_z,n1_x,n1_z,n2_x,n2_z,reason");
			foreach (var row in _cornerSkips)
			{
				cornersBuilder.AppendLine(row);
			}
			File.WriteAllText(Path.Combine(debugPath, "dungeon_wall_corner_skipped.csv"), cornersBuilder.ToString(), Encoding.UTF8);
		}
	}

	private void RecordCornerSkip(Vector2I pos, Vector2I facingDir, int[,] grid, string reason)
	{
		var neighbors = FloorNeighbors(pos, grid);
		if (neighbors.Count != 2 || !IsCornerPair(neighbors))
		{
			return;
		}

		_cornerSkips.Add(
			$"{pos.X},{pos.Y},{facingDir.X},{facingDir.Y},{neighbors[0].X},{neighbors[0].Y},{neighbors[1].X},{neighbors[1].Y},{reason}"
		);
	}

	private void PlaceWallTile(Vector2I pos, Vector2I facingDir, int[,] grid)
	{
		var floorNeighbors = FloorNeighbors(pos, grid);
		if (floorNeighbors.Count == 0)
		{
			return;
		}

		var tileId = _wallId;
		var orientation = OrientationFromDir(facingDir);

		if (floorNeighbors.Count == 4 && _wallCrossingId >= 0)
		{
			tileId = _wallCrossingId;
			orientation = OrientationFromDir(floorNeighbors[0]);
		}
		else if (floorNeighbors.Count == 3 && _wallTsplitId >= 0)
		{
			tileId = _wallTsplitId;
			orientation = OrientationForTsplit(floorNeighbors);
		}
		else if (floorNeighbors.Count == 2 && IsCornerPair(floorNeighbors))
		{
			tileId = _wallCornerId >= 0 ? _wallCornerId : tileId;
			orientation = OrientationForCorner(floorNeighbors);
		}
		else
		{
			var adjacent = pos + floorNeighbors[0];
			if (IsDeadEnd(grid, adjacent))
			{
				tileId = _wallEndcapId >= 0 ? _wallEndcapId : tileId;
				orientation = OrientationFromDir(floorNeighbors[0]);
			}
			else
			{
				tileId = PickWallStraightAsset(grid, adjacent, tileId);
				if (_rng.Randf() < WallVariantChance)
				{
					tileId = PickFrom(_wallVariantIds, tileId);
				}
				orientation = OrientationFromDir(floorNeighbors[0]);
			}
		}

		if (tileId >= 0)
		{
			_gridMap.SetCellItem(new Vector3I(pos.X, 0, pos.Y), tileId, orientation);
		}
	}

	private List<Vector2I> FloorNeighbors(Vector2I pos, int[,] grid)
	{
		var neighbors = new List<Vector2I>();
		foreach (var dir in CardinalDirs())
		{
			var check = pos + dir;
		if (InBounds(check) && grid[check.X, check.Y] != (int)CellType.Empty)
		{
			neighbors.Add(dir);
		}
	}
	return neighbors;
	}

	private static bool IsCornerPair(List<Vector2I> dirs)
	{
		if (dirs.Count != 2)
		{
			return false;
		}

		return dirs[0].X != dirs[1].X && dirs[0].Y != dirs[1].Y;
	}

	private int OrientationForCorner(List<Vector2I> dirs)
	{
		var a = dirs[0];
		var b = dirs[1];
		var pair = a + b;

		if (pair == new Vector2I(1, 1))
		{
			return OrientationFromDir(new Vector2I(1, 0));
		}
		if (pair == new Vector2I(1, -1))
		{
			return OrientationFromDir(new Vector2I(0, -1));
		}
		if (pair == new Vector2I(-1, -1))
		{
			return OrientationFromDir(new Vector2I(-1, 0));
		}

		return OrientationFromDir(new Vector2I(0, 1));
	}

	private int OrientationForTsplit(List<Vector2I> dirs)
	{
		var mask = new HashSet<Vector2I>(dirs);
		if (!mask.Contains(new Vector2I(0, -1)))
		{
			return OrientationFromDir(new Vector2I(0, -1));
		}
		if (!mask.Contains(new Vector2I(1, 0)))
		{
			return OrientationFromDir(new Vector2I(1, 0));
		}
		if (!mask.Contains(new Vector2I(0, 1)))
		{
			return OrientationFromDir(new Vector2I(0, 1));
		}
		return OrientationFromDir(new Vector2I(-1, 0));
	}

	private static Vector2I[] CardinalDirs()
	{
		return new[]
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1)
		};
	}

	private int OrientationFromDir(Vector2I dir)
	{
		var rotation = 0.0f;
		if (dir == new Vector2I(1, 0))
		{
			rotation = 0.0f;
		}
		else if (dir == new Vector2I(-1, 0))
		{
			rotation = Mathf.Pi;
		}
		else if (dir == new Vector2I(0, -1))
		{
			rotation = -Mathf.Pi / 2.0f;
		}
		else
		{
			rotation = Mathf.Pi / 2.0f;
		}

		var offset = Mathf.DegToRad(WallRotationOffsetDegrees);
		return _gridMap.GetOrthogonalIndexFromBasis(new Basis(Vector3.Up, rotation + offset));
	}

	private bool InBounds(Vector2I pos)
	{
		return pos.X >= 0 && pos.Y >= 0 && pos.X < GridSize.X && pos.Y < GridSize.Y;
	}

	private bool IsDeadEnd(int[,] grid, Vector2I floorCell)
	{
		var neighbors = 0;
		foreach (var dir in CardinalDirs())
		{
			var check = floorCell + dir;
			if (InBounds(check) && grid[check.X, check.Y] != (int)CellType.Empty)
			{
				neighbors++;
			}
		}
		return neighbors <= 1;
	}

	private int PickSmallTile(bool preferRoom)
	{
		var source = _floorSmallIds;
		if (preferRoom && _floorWoodSmallIds.Count > 0 && _rng.Randf() < 0.2f)
		{
			source = _floorWoodSmallIds;
		}
		return PickFrom(source);
	}

	private int PickLargeTile(bool useWood)
	{
		var source = useWood ? _floorWoodLargeIds : _floorLargeIds;
		if (source.Count == 0)
		{
			source = _floorLargeIds;
		}
		return PickFrom(source);
	}

	private int PickWallStraightAsset(int[,] grid, Vector2I adjacentFloor, int fallback)
	{
		var isRoomBoundary = grid[adjacentFloor.X, adjacentFloor.Y] == (int)CellType.Room ||
			grid[adjacentFloor.X, adjacentFloor.Y] == (int)CellType.Special;
		if (isRoomBoundary && _wallDoorwayIds.Count > 0 && _rng.Randf() < DoorChance)
		{
			return PickFrom(_wallDoorwayIds, fallback);
		}
		if (isRoomBoundary && _wallWindowIds.Count > 0 && _rng.Randf() < WindowChance)
		{
			return PickFrom(_wallWindowIds, fallback);
		}
		if (isRoomBoundary && _wallArchedIds.Count > 0 && _rng.Randf() < ArchedChance)
		{
			return PickFrom(_wallArchedIds, fallback);
		}
		return fallback;
	}

	private bool CanPlaceBlock(int[,] grid, bool[,] occupied, Vector2I start, int size)
	{
		for (var dx = 0; dx < size; dx++)
		{
			for (var dz = 0; dz < size; dz++)
			{
				var x = start.X + dx;
				var z = start.Y + dz;
				if (!InBounds(new Vector2I(x, z)))
				{
					return false;
				}
				if (grid[x, z] == (int)CellType.Empty || occupied[x, z])
				{
					return false;
				}
			}
		}
		return true;
	}

	private void PlaceBlockTile(bool[,] occupied, Vector2I start, int size, int tileId)
	{
		if (tileId < 0)
		{
			return;
		}
		_gridMap.SetCellItem(new Vector3I(start.X, 0, start.Y), tileId);
		for (var dx = 0; dx < size; dx++)
		{
			for (var dz = 0; dz < size; dz++)
			{
				occupied[start.X + dx, start.Y + dz] = true;
			}
		}
	}

	private HashSet<int> PickRoomIndices(List<Rect2I> rooms, int count)
	{
		var result = new HashSet<int>();
		if (count <= 0 || rooms.Count == 0)
		{
			return result;
		}
		var tries = 0;
		while (result.Count < Mathf.Min(count, rooms.Count) && tries < rooms.Count * 4)
		{
			var index = _rng.RandiRange(0, rooms.Count - 1);
			result.Add(index);
			tries++;
		}
		return result;
	}

	private int PickFrom(List<int> items, int fallback = -1)
	{
		if (items.Count == 0)
		{
			return fallback;
		}
		return items[_rng.RandiRange(0, items.Count - 1)];
	}

	private MeshLibrary BuildMeshLibrary()
	{
		var library = new MeshLibrary();
		_floorSmallIds.Clear();
		_floorLargeIds.Clear();
		_floorExtralargeIds.Clear();
		_floorWoodSmallIds.Clear();
		_floorWoodLargeIds.Clear();
		_floorSmallVariantIds.Clear();
		_floorLargeVariantIds.Clear();
		_floorTrapIds.Clear();
		_wallId = -1;
		_wallCornerId = -1;
		_wallEndcapId = -1;
		_wallTsplitId = -1;
		_wallCrossingId = -1;
		_wallVariantIds.Clear();
		_wallDoorwayIds.Clear();
		_wallWindowIds.Clear();
		_wallArchedIds.Clear();

		AddTileItems(library, FloorSmallAssets, _floorSmallIds);
		AddTileItems(library, FloorLargeAssets, _floorLargeIds);
		AddTileItems(library, FloorExtralargeAssets, _floorExtralargeIds);
		AddTileItems(library, FloorWoodSmallAssets, _floorWoodSmallIds);
		AddTileItems(library, FloorWoodLargeAssets, _floorWoodLargeIds);
		AddTileItems(library, FloorSmallVariantAssets, _floorSmallVariantIds);
		AddTileItems(library, FloorLargeVariantAssets, _floorLargeVariantIds);
		AddTileItems(library, FloorTrapAssets, _floorTrapIds);

		_wallId = AddTileItem(library, WallAsset);
		_wallCornerId = AddTileItem(library, WallCornerAsset);
		_wallEndcapId = AddTileItem(library, WallEndcapAsset);
		_wallTsplitId = AddTileItem(library, WallTsplitAsset);
		_wallCrossingId = AddTileItem(library, WallCrossingAsset);
		AddTileItems(library, WallVariantAssets, _wallVariantIds);
		AddTileItems(library, WallDoorwayAssets, _wallDoorwayIds);
		AddTileItems(library, WallWindowAssets, _wallWindowIds);
		AddTileItems(library, WallArchedAssets, _wallArchedIds);

		return library;
	}

	private int AddTileItem(MeshLibrary library, string assetName)
	{
		var tileId = library.GetLastUnusedItemId();
		var mesh = LoadMeshFromGltf(AssetPath(assetName));
		if (mesh == null)
		{
			GD.PushWarning($"Missing mesh: {assetName}");
			return -1;
		}

		library.CreateItem(tileId);
		library.SetItemName(tileId, assetName.GetBaseName());
		library.SetItemMesh(tileId, mesh);
		return tileId;
	}

	private void AddTileItems(MeshLibrary library, string[] assets, List<int> output)
	{
		foreach (var asset in assets)
		{
			var id = AddTileItem(library, asset);
			if (id >= 0)
			{
				output.Add(id);
			}
		}
	}

	private string AssetPath(string fileName)
	{
		return $"{GltfDir.TrimEnd('/')}/{fileName}";
	}

	private Mesh LoadMeshFromGltf(string path)
	{
		var resource = GD.Load(path);
		switch (resource)
		{
			case Mesh mesh:
				return mesh;
			case PackedScene scene:
			{
				using var instance = scene.Instantiate();
				var meshInstance = FindMeshInstance(instance);
				return meshInstance?.Mesh;
			}
			default:
				return null;
		}
	}

	private MeshInstance3D FindMeshInstance(Node node)
	{
		if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
		{
			return meshInstance;
		}

		foreach (var child in node.GetChildren())
		{
			if (child is Node childNode)
			{
				var result = FindMeshInstance(childNode);
				if (result != null)
				{
					return result;
				}
			}
		}

		return null;
	}
}
