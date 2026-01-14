using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class DungeonGenerator : Node3D
{
    [Export] public string GltfDir = "res://Assets/Map/kaykit_dungeon/Assets/gltf";

    [Export] public Vector3 CellSize = new(2.0f, 2.0f, 2.0f);
    [Export] public bool RandomSeed = true;
    [Export] public int Seed = 0;

    [Export] public int RoomCount = 8;
    [Export] public Vector2I MinRoomSize = new(4, 4);
    [Export] public Vector2I MaxRoomSize = new(10, 10);
    [Export] public Vector2I GridSize = new(48, 48);
    [Export] public int CorridorWidth = 1;

    [Export] public string[] FloorAssets =
    {
        "floor_tile_large.gltf",
        "floor_dirt_large.gltf",
        "floor_tile_small.gltf"
    };
    [Export] public string WallAsset = "wall.gltf";
    [Export] public string WallCornerAsset = "wall_corner.gltf";
    [Export] public string WallEndcapAsset = "wall_endcap.gltf";

    [Export] public bool RegenerateOnReady = true;

    [Export] public bool EditorGenerate = false;
    [Export] public bool EditorClear = false;

    private enum CellType
    {
        Empty = 0,
        Floor = 1
    }

    private GridMap _gridMap;
    private readonly RandomNumberGenerator _rng = new();
    private readonly List<int> _floorTileIds = new();
    private int _wallId = -1;
    private int _wallCornerId = -1;
    private int _wallEndcapId = -1;

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
        ConnectRooms(grid, rooms);
        PlaceTiles(grid);
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
                grid[x, z] = (int)CellType.Floor;
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
        grid[x, z] = (int)CellType.Floor;

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
                if (InBounds(new Vector2I(x, z)))
                {
                    grid[x, z] = (int)CellType.Floor;
                }
            }
        }
    }

    private void PlaceTiles(int[,] grid)
    {
        var walls = new Dictionary<Vector2I, Vector2I>();
        for (var x = 0; x < GridSize.X; x++)
        {
            for (var z = 0; z < GridSize.Y; z++)
            {
                if (grid[x, z] != (int)CellType.Floor)
                {
                    continue;
                }

                var floorId = RandomFloorTile();
                _gridMap.SetCellItem(new Vector3I(x, 0, z), floorId);

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
            PlaceWallTile(entry.Key, entry.Value, grid);
        }
    }

    private void PlaceWallTile(Vector2I pos, Vector2I facingDir, int[,] grid)
    {
        var floorNeighbors = FloorNeighbors(pos, grid);
        var tileId = _wallId;
        var orientation = OrientationFromDir(facingDir);

        if (floorNeighbors.Count == 1)
        {
            tileId = _wallEndcapId >= 0 ? _wallEndcapId : tileId;
            orientation = OrientationFromDir(floorNeighbors[0]);
        }
        else if (floorNeighbors.Count == 2 && IsCornerPair(floorNeighbors))
        {
            tileId = _wallCornerId >= 0 ? _wallCornerId : tileId;
            orientation = OrientationForCorner(floorNeighbors);
        }

        if (tileId < 0)
        {
            return;
        }

        _gridMap.SetCellItem(new Vector3I(pos.X, 0, pos.Y), tileId, orientation);
    }

    private List<Vector2I> FloorNeighbors(Vector2I pos, int[,] grid)
    {
        var neighbors = new List<Vector2I>();
        foreach (var dir in CardinalDirs())
        {
            var check = pos + dir;
            if (InBounds(check) && grid[check.X, check.Y] == (int)CellType.Floor)
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

        return _gridMap.GetOrthogonalIndexFromBasis(new Basis(Vector3.Up, rotation));
    }

    private bool InBounds(Vector2I pos)
    {
        return pos.X >= 0 && pos.Y >= 0 && pos.X < GridSize.X && pos.Y < GridSize.Y;
    }

    private int RandomFloorTile()
    {
        if (_floorTileIds.Count == 0)
        {
            return -1;
        }
        return _floorTileIds[_rng.RandiRange(0, _floorTileIds.Count - 1)];
    }

    private MeshLibrary BuildMeshLibrary()
    {
        var library = new MeshLibrary();
        _floorTileIds.Clear();
        _wallId = -1;
        _wallCornerId = -1;
        _wallEndcapId = -1;

        foreach (var floorAsset in FloorAssets)
        {
            var floorId = library.GetLastUnusedItemId();
            var mesh = LoadMeshFromGltf(AssetPath(floorAsset));
            if (mesh == null)
            {
                GD.PushWarning($"Missing floor mesh: {floorAsset}");
                continue;
            }
            library.CreateItem(floorId);
            library.SetItemName(floorId, floorAsset.GetBaseName());
            library.SetItemMesh(floorId, mesh);
            _floorTileIds.Add(floorId);
        }

        _wallId = AddTileItem(library, WallAsset);
        _wallCornerId = AddTileItem(library, WallCornerAsset);
        _wallEndcapId = AddTileItem(library, WallEndcapAsset);

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
