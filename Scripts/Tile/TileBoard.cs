using Godot;
using System.Collections.Generic;

public partial class TileBoard : Node3D
{
    [Export] public NodePath DungeonPath;
    [Export] public NodePath UnitsRootPath;
    [Export] public NodePath CommandPanelPath;
    [Export] public int TilesPerFloorCell = 1;
    [Export] public float OverlayHeight = 0.05f;
    [Export] public float SelectionHeightOffset = 0.02f;
    [Export] public int AttackRadius = 2;
    [Export] public bool ShowBaseGrid = true;

    private DungeonGenerator _dungeon;
    private GridMap _gridMap;
    private Node _unitsRoot;
    private CommandPanel _commandPanel;

    private Vector2I _gridSize;
    private float _tileSize;
    private Vector3 _origin;

    private Vector2I _selectedTile;
    private UnitActor _selectedUnit;
    private bool _showMoveRange;
    private bool _showAttackPreview;

    private MultiMeshInstance3D _baseOverlay;
    private MultiMeshInstance3D _moveOverlay;
    private MultiMeshInstance3D _attackOverlay;
    private MeshInstance3D _selectionOverlay;

    private readonly List<UnitActor> _units = new();
    private readonly Dictionary<Vector2I, UnitActor> _occupancy = new();

    public override void _Ready()
    {
        CallDeferred(nameof(Initialize));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Left:
                MoveSelection(new Vector2I(-1, 0));
                break;
            case Key.Right:
                MoveSelection(new Vector2I(1, 0));
                break;
            case Key.Up:
                MoveSelection(new Vector2I(0, -1));
                break;
            case Key.Down:
                MoveSelection(new Vector2I(0, 1));
                break;
            case Key.Space:
                TrySelectUnit();
                break;
            case Key.Escape:
                ClearSelection();
                break;
            case Key.M:
                ShowMoveRange();
                break;
            case Key.X:
                ToggleAttackPreview();
                break;
        }
    }

    private void Initialize()
    {
        _dungeon = GetNodeOrNull<DungeonGenerator>(DungeonPath);
        if (_dungeon == null)
        {
            GD.PushWarning("TileBoard: DungeonGenerator not found.");
            return;
        }

        _gridMap = _dungeon.GetNodeOrNull<GridMap>("GridMap");
        _unitsRoot = UnitsRootPath.IsEmpty ? null : GetNodeOrNull<Node>(UnitsRootPath);
        _commandPanel = CommandPanelPath.IsEmpty ? null : GetNodeOrNull<CommandPanel>(CommandPanelPath);

        if (_commandPanel != null)
        {
            _commandPanel.MovePressed += OnMovePressed;
            _commandPanel.SkillPressed += OnSkillPressed;
            _commandPanel.SetSkillEnabled(false);
            _commandPanel.ShowFor(null);
        }

        var cellSize = _gridMap != null ? _gridMap.CellSize : _dungeon.CellSize;
        _tileSize = cellSize.X / Mathf.Max(1, TilesPerFloorCell);
        _gridSize = new Vector2I(_dungeon.GridSize.X * TilesPerFloorCell, _dungeon.GridSize.Y * TilesPerFloorCell);
        _origin = _gridMap != null ? _gridMap.GlobalTransform.Origin : _dungeon.GlobalTransform.Origin;

        BuildOverlays();
        RefreshUnits();

        _selectedTile = GetInitialSelection();
        UpdateSelectionVisual();
    }

    private void BuildOverlays()
    {
        _baseOverlay = CreateOverlay("BaseOverlay", new Color(0.2f, 0.8f, 0.2f, 0.15f));
        _moveOverlay = CreateOverlay("MoveOverlay", new Color(0.2f, 0.6f, 1.0f, 0.35f));
        _attackOverlay = CreateOverlay("AttackOverlay", new Color(1.0f, 0.2f, 0.2f, 0.35f));
        _selectionOverlay = CreateSelectionOverlay("SelectionOverlay", new Color(1.0f, 1.0f, 0.2f, 0.5f));

        if (_baseOverlay != null)
        {
            _baseOverlay.Visible = ShowBaseGrid;
            FillBaseOverlay();
        }

        if (_moveOverlay != null)
        {
            _moveOverlay.Visible = false;
        }

        if (_attackOverlay != null)
        {
            _attackOverlay.Visible = false;
        }
    }

    private void RefreshUnits()
    {
        _units.Clear();
        _occupancy.Clear();

        var nodes = GetTree().GetNodesInGroup(UnitActor.UnitGroup);
        foreach (var node in nodes)
        {
            if (node is not UnitActor unit)
            {
                continue;
            }

            if (_unitsRoot != null && !_unitsRoot.IsAncestorOf(unit))
            {
                continue;
            }

            _units.Add(unit);
            SnapUnitToGrid(unit);
        }

        RebuildOccupancy();
    }

    private void RebuildOccupancy()
    {
        _occupancy.Clear();

        foreach (var unit in _units)
        {
            foreach (var tile in unit.GetFootprintTiles(unit.TilePosition))
            {
                if (IsWithinGrid(tile))
                {
                    _occupancy[tile] = unit;
                }
            }
        }
    }

    private Vector2I GetInitialSelection()
    {
        if (_units.Count > 0)
        {
            return ClampToGrid(_units[0].TilePosition);
        }

        return ClampToGrid(new Vector2I(_gridSize.X / 2, _gridSize.Y / 2));
    }

    private void MoveSelection(Vector2I delta)
    {
        _selectedTile = ClampToGrid(_selectedTile + delta);
        UpdateSelectionVisual();
        UpdateAttackOverlay();
    }

    private void UpdateSelectionVisual()
    {
        if (_selectionOverlay == null)
        {
            return;
        }

        _selectionOverlay.Visible = true;
        _selectionOverlay.GlobalPosition = TileToWorld(_selectedTile, OverlayHeight + SelectionHeightOffset);
    }

    private void TrySelectUnit()
    {
        if (_occupancy.TryGetValue(_selectedTile, out var unit))
        {
            _selectedUnit = unit;
            if (_commandPanel != null)
            {
                _commandPanel.ShowFor(unit);
            }
        }
    }

    private void ClearSelection()
    {
        _selectedUnit = null;
        _showMoveRange = false;
        _showAttackPreview = false;

        if (_commandPanel != null)
        {
            _commandPanel.ShowFor(null);
        }

        if (_moveOverlay != null)
        {
            _moveOverlay.Visible = false;
        }

        if (_attackOverlay != null)
        {
            _attackOverlay.Visible = false;
        }
    }

    private void ShowMoveRange()
    {
        if (_selectedUnit == null || _moveOverlay == null)
        {
            return;
        }

        _showMoveRange = true;
        UpdateMoveOverlay();
    }

    private void ToggleAttackPreview()
    {
        if (_attackOverlay == null)
        {
            return;
        }

        _showAttackPreview = !_showAttackPreview;
        UpdateAttackOverlay();
    }

    private void OnMovePressed()
    {
        ShowMoveRange();
    }

    private void OnSkillPressed()
    {
        GD.Print("TileBoard: Skill command is not implemented.");
    }

    private void UpdateMoveOverlay()
    {
        if (_moveOverlay == null)
        {
            return;
        }

        if (!_showMoveRange || _selectedUnit == null)
        {
            _moveOverlay.Visible = false;
            return;
        }

        var reachableAnchors = ComputeReachableAnchors(_selectedUnit);
        var tiles = new HashSet<Vector2I>();

        foreach (var anchor in reachableAnchors)
        {
            foreach (var tile in _selectedUnit.GetFootprintTiles(anchor))
            {
                if (IsWithinGrid(tile))
                {
                    tiles.Add(tile);
                }
            }
        }

        SetOverlayTiles(_moveOverlay, tiles);
        _moveOverlay.Visible = true;
    }

    private void UpdateAttackOverlay()
    {
        if (_attackOverlay == null)
        {
            return;
        }

        if (!_showAttackPreview)
        {
            _attackOverlay.Visible = false;
            return;
        }

        var tiles = new HashSet<Vector2I>();
        for (int dx = -AttackRadius; dx <= AttackRadius; dx++)
        {
            for (int dy = -AttackRadius; dy <= AttackRadius; dy++)
            {
                var dist = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (dist > AttackRadius)
                {
                    continue;
                }

                var tile = new Vector2I(_selectedTile.X + dx, _selectedTile.Y + dy);
                if (IsWithinGrid(tile))
                {
                    tiles.Add(tile);
                }
            }
        }

        SetOverlayTiles(_attackOverlay, tiles);
        _attackOverlay.Visible = true;
    }

    private HashSet<Vector2I> ComputeReachableAnchors(UnitActor unit)
    {
        var reachable = new HashSet<Vector2I>();
        var frontier = new Queue<(Vector2I pos, int cost)>();

        var start = ClampAnchor(unit.TilePosition, unit.Size);
        frontier.Enqueue((start, 0));
        reachable.Add(start);

        var directions = new Vector2I[]
        {
            new(-1, 0),
            new(1, 0),
            new(0, -1),
            new(0, 1)
        };

        while (frontier.Count > 0)
        {
            var (pos, cost) = frontier.Dequeue();
            if (cost >= unit.MovePoints)
            {
                continue;
            }

            foreach (var dir in directions)
            {
                var next = pos + dir;
                if (reachable.Contains(next))
                {
                    continue;
                }

                if (!IsAnchorValid(next, unit))
                {
                    continue;
                }

                reachable.Add(next);
                frontier.Enqueue((next, cost + 1));
            }
        }

        return reachable;
    }

    private bool IsAnchorValid(Vector2I anchor, UnitActor unit)
    {
        if (!IsAnchorWithinGrid(anchor, unit.Size))
        {
            return false;
        }

        foreach (var tile in unit.GetFootprintTiles(anchor))
        {
            if (_occupancy.TryGetValue(tile, out var occupant) && occupant != unit)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsAnchorWithinGrid(Vector2I anchor, Vector2I size)
    {
        return anchor.X >= 0 && anchor.Y >= 0 &&
               anchor.X + size.X - 1 < _gridSize.X &&
               anchor.Y + size.Y - 1 < _gridSize.Y;
    }

    private bool IsWithinGrid(Vector2I tile)
    {
        return tile.X >= 0 && tile.Y >= 0 && tile.X < _gridSize.X && tile.Y < _gridSize.Y;
    }

    private Vector2I ClampToGrid(Vector2I tile)
    {
        return new Vector2I(
            Mathf.Clamp(tile.X, 0, _gridSize.X - 1),
            Mathf.Clamp(tile.Y, 0, _gridSize.Y - 1));
    }

    private Vector2I ClampAnchor(Vector2I anchor, Vector2I size)
    {
        return new Vector2I(
            Mathf.Clamp(anchor.X, 0, Mathf.Max(0, _gridSize.X - size.X)),
            Mathf.Clamp(anchor.Y, 0, Mathf.Max(0, _gridSize.Y - size.Y)));
    }

    private void SnapUnitToGrid(UnitActor unit)
    {
        var center = AnchorToWorld(unit.TilePosition, unit.Size);
        unit.GlobalPosition = new Vector3(center.X, _origin.Y, center.Z);
    }

    private Vector3 TileToWorld(Vector2I tile, float heightOffset)
    {
        return new Vector3(
            _origin.X + (tile.X + 0.5f) * _tileSize,
            _origin.Y + heightOffset,
            _origin.Z + (tile.Y + 0.5f) * _tileSize);
    }

    private Vector3 AnchorToWorld(Vector2I anchor, Vector2I size)
    {
        return new Vector3(
            _origin.X + (anchor.X + size.X * 0.5f) * _tileSize,
            _origin.Y,
            _origin.Z + (anchor.Y + size.Y * 0.5f) * _tileSize);
    }

    private MultiMeshInstance3D CreateOverlay(string name, Color color)
    {
        var mesh = new PlaneMesh
        {
            Size = new Vector2(_tileSize, _tileSize)
        };

        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = 0
        };

        var instance = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multimesh,
            MaterialOverride = material
        };

        AddChild(instance);
        return instance;
    }

    private MeshInstance3D CreateSelectionOverlay(string name, Color color)
    {
        var mesh = new PlaneMesh
        {
            Size = new Vector2(_tileSize, _tileSize)
        };

        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        var instance = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material
        };

        AddChild(instance);
        return instance;
    }

    private void FillBaseOverlay()
    {
        if (_baseOverlay == null)
        {
            return;
        }

        var multimesh = _baseOverlay.Multimesh;
        if (multimesh == null)
        {
            return;
        }

        var count = _gridSize.X * _gridSize.Y;
        multimesh.InstanceCount = count;

        int index = 0;
        for (int y = 0; y < _gridSize.Y; y++)
        {
            for (int x = 0; x < _gridSize.X; x++)
            {
                var position = TileToWorld(new Vector2I(x, y), OverlayHeight);
                multimesh.SetInstanceTransform(index, new Transform3D(Basis.Identity, position));
                index++;
            }
        }
    }

    private void SetOverlayTiles(MultiMeshInstance3D overlay, HashSet<Vector2I> tiles)
    {
        if (overlay == null || overlay.Multimesh == null)
        {
            return;
        }

        overlay.Multimesh.InstanceCount = tiles.Count;

        int index = 0;
        foreach (var tile in tiles)
        {
            var position = TileToWorld(tile, OverlayHeight);
            overlay.Multimesh.SetInstanceTransform(index, new Transform3D(Basis.Identity, position));
            index++;
        }
    }
}
