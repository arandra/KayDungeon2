using Godot;
using System.Collections.Generic;

public partial class TileBoard : Node3D
{
    [Export] public NodePath DungeonPath;
    [Export] public NodePath UnitsRootPath;
    [Export] public NodePath CommandPanelPath;
    [Export] public int TilesPerFloorCell = 4;
    [Export] public Vector2I DefaultSelectionSize = new(2, 2);
    [Export] public float OverlayHeight = 0.05f;
    [Export] public float SelectionHeightOffset = 0.02f;
    [Export] public float MoveSecondsPerTile = 0.2f;
    [Export] public int AttackRadius = 2;
    [Export] public bool ShowBaseGrid = true;

    private enum ControlMode
    {
        SelectTile,
        CommandSelect,
        MoveTarget
    }

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
    private ControlMode _mode = ControlMode.SelectTile;
    private int _commandIndex;
    private bool _isMoving;
    private Vector2I _selectionSize = new(1, 1);
    private HashSet<Vector2I> _reachableAnchors = new();
    private Dictionary<Vector2I, Vector2I> _pathParents = new();

    private MultiMeshInstance3D _baseOverlay;
    private MultiMeshInstance3D _moveOverlay;
    private MultiMeshInstance3D _attackOverlay;
    private MeshInstance3D _selectionOverlay;
    private MeshInstance3D _pathLine;

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
                HandleDirectionalInput(new Vector2I(-1, 0));
                break;
            case Key.Right:
                HandleDirectionalInput(new Vector2I(1, 0));
                break;
            case Key.Up:
                HandleDirectionalInput(new Vector2I(0, -1));
                break;
            case Key.Down:
                HandleDirectionalInput(new Vector2I(0, 1));
                break;
            case Key.Space:
                HandleConfirm();
                break;
            case Key.Escape:
                HandleCancel();
                break;
            case Key.X:
                ToggleAttackPreview();
                break;
        }
    }

    private void HandleDirectionalInput(Vector2I delta)
    {
        if (_isMoving)
        {
            return;
        }

        if (_mode == ControlMode.CommandSelect)
        {
            CycleCommandSelection(delta);
            return;
        }

        MoveSelection(delta);
    }

    private void HandleConfirm()
    {
        if (_isMoving)
        {
            return;
        }

        if (_mode == ControlMode.SelectTile)
        {
            TrySelectUnit();
            return;
        }

        if (_mode == ControlMode.CommandSelect)
        {
            ConfirmCommandSelection();
            return;
        }

        if (_mode == ControlMode.MoveTarget)
        {
            ConfirmMoveTarget();
        }
    }

    private void HandleCancel()
    {
        if (_isMoving)
        {
            return;
        }

        if (_mode == ControlMode.MoveTarget)
        {
            CancelMoveCommand();
            return;
        }

        if (_mode == ControlMode.CommandSelect)
        {
            DeselectUnit();
            return;
        }

        DeselectUnit();
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
            _commandPanel.SetSkillEnabled(true);
            _commandPanel.ShowFor(null);
        }

        var cellSize = _gridMap != null ? _gridMap.CellSize : _dungeon.CellSize;
        _tileSize = cellSize.X / Mathf.Max(1, TilesPerFloorCell);
        _gridSize = new Vector2I(_dungeon.GridSize.X * TilesPerFloorCell, _dungeon.GridSize.Y * TilesPerFloorCell);
        _origin = _gridMap != null ? _gridMap.GlobalTransform.Origin : _dungeon.GlobalTransform.Origin;

        BuildOverlays();
        RefreshUnits();

        _selectedTile = GetInitialSelection();
        _mode = ControlMode.SelectTile;
        UpdateSelectionVisual();
    }

    private void BuildOverlays()
    {
        _baseOverlay = CreateOverlay("BaseOverlay", new Color(0.2f, 0.8f, 0.2f, 0.15f));
        _moveOverlay = CreateOverlay("MoveOverlay", new Color(0.2f, 0.6f, 1.0f, 0.35f));
        _attackOverlay = CreateOverlay("AttackOverlay", new Color(1.0f, 0.2f, 0.2f, 0.35f));
        _selectionOverlay = CreateSelectionOverlay("SelectionOverlay", new Color(1.0f, 1.0f, 0.2f, 0.5f));
        _pathLine = CreatePathLine("MovePathLine", new Color(1.0f, 0.9f, 0.2f, 1.0f));

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

        if (_pathLine != null)
        {
            _pathLine.Visible = false;
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
        }

        PlaceUnitsOnFloor();
    }

    private void PlaceUnitsOnFloor()
    {
        _occupancy.Clear();

        foreach (var unit in _units)
        {
            var start = ClampAnchor(unit.TilePosition, unit.Size);
            var anchor = FindNearestValidAnchor(unit, start);
            unit.TilePosition = anchor;
            SnapUnitToGrid(unit);

            foreach (var tile in unit.GetFootprintTiles(anchor))
            {
                if (IsWithinGrid(tile))
                {
                    _occupancy[tile] = unit;
                }
            }
        }
    }

    private Vector2I FindNearestValidAnchor(UnitActor unit, Vector2I start)
    {
        var startAnchor = ClampAnchor(start, unit.Size);
        if (IsAnchorValid(startAnchor, unit))
        {
            return startAnchor;
        }

        var visited = new HashSet<Vector2I>();
        var queue = new Queue<Vector2I>();
        queue.Enqueue(startAnchor);
        visited.Add(startAnchor);

        var directions = new Vector2I[]
        {
            new(-1, 0),
            new(1, 0),
            new(0, -1),
            new(0, 1)
        };

        while (queue.Count > 0)
        {
            var anchor = queue.Dequeue();
            foreach (var dir in directions)
            {
                var next = anchor + dir;
                if (!IsAnchorWithinGrid(next, unit.Size) || visited.Contains(next))
                {
                    continue;
                }

                if (IsAnchorValid(next, unit))
                {
                    return next;
                }

                visited.Add(next);
                queue.Enqueue(next);
            }
        }

        return startAnchor;
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
            return ClampSelectionAnchor(_units[0].TilePosition, _units[0].Size);
        }

        return ClampSelectionAnchor(new Vector2I(_gridSize.X / 2, _gridSize.Y / 2), DefaultSelectionSize);
    }

    private void MoveSelection(Vector2I delta)
    {
        _selectedTile = ClampSelectionAnchor(_selectedTile + delta, GetSelectionSize());
        UpdateSelectionVisual();
        UpdatePathLine();
        UpdateAttackOverlay();
    }

    private void UpdateSelectionVisual()
    {
        if (_selectionOverlay == null)
        {
            return;
        }

        var size = GetSelectionSize();
        if (size.X <= 0 || size.Y <= 0)
        {
            return;
        }

        if (size != _selectionSize)
        {
            _selectionSize = size;
            _selectionOverlay.Scale = new Vector3(size.X, 1.0f, size.Y);
        }

        _selectionOverlay.Visible = true;
        var center = AnchorToWorld(_selectedTile, size);
        _selectionOverlay.GlobalPosition = new Vector3(center.X, _origin.Y + OverlayHeight + SelectionHeightOffset, center.Z);
    }

    private void TrySelectUnit()
    {
        if (_mode != ControlMode.SelectTile)
        {
            return;
        }

        if (_occupancy.TryGetValue(_selectedTile, out var unit))
        {
            _selectedUnit = unit;
            _selectedTile = ClampSelectionAnchor(unit.TilePosition, unit.Size);
            _mode = ControlMode.CommandSelect;
            _commandIndex = 0;
            if (_commandPanel != null)
            {
                _commandPanel.ShowFor(unit);
                _commandPanel.SetSelection(_commandIndex);
            }
            UpdateSelectionVisual();
        }
    }

    private void DeselectUnit()
    {
        _selectedUnit = null;
        _showMoveRange = false;
        _showAttackPreview = false;
        _mode = ControlMode.SelectTile;
        _commandIndex = 0;

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

        if (_pathLine != null)
        {
            _pathLine.Visible = false;
        }

        _selectedTile = ClampSelectionAnchor(_selectedTile, DefaultSelectionSize);
        UpdateSelectionVisual();
    }

    private void CancelMoveCommand()
    {
        _showMoveRange = false;
        _mode = ControlMode.CommandSelect;

        if (_moveOverlay != null)
        {
            _moveOverlay.Visible = false;
        }
        if (_pathLine != null)
        {
            _pathLine.Visible = false;
        }
        UpdateSelectionVisual();
        if (_commandPanel != null)
        {
            _commandPanel.SetSelection(_commandIndex);
        }
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
        EnterMoveMode();
    }

    private void OnSkillPressed()
    {
        GD.Print("TileBoard: Skill command is not implemented.");
    }

    private void CycleCommandSelection(Vector2I delta)
    {
        if (_commandPanel == null)
        {
            return;
        }

        var step = delta.X + delta.Y;
        if (step == 0)
        {
            return;
        }

        var commandCount = 2;
        var direction = step > 0 ? 1 : -1;
        _commandIndex = (_commandIndex + direction + commandCount) % commandCount;
        _commandPanel.SetSelection(_commandIndex);
    }

    private void ConfirmCommandSelection()
    {
        if (_selectedUnit == null)
        {
            return;
        }

        if (_commandIndex == 0)
        {
            EnterMoveMode();
        }
        else
        {
            OnSkillPressed();
        }
    }

    private void ConfirmMoveTarget()
    {
        if (_selectedUnit == null)
        {
            return;
        }

        if (!_reachableAnchors.Contains(_selectedTile))
        {
            return;
        }

        var path = BuildPath(_selectedTile);
        if (path == null || path.Count == 0)
        {
            return;
        }

        MoveUnitAlongPath(_selectedUnit, path);
    }

    private void EnterMoveMode()
    {
        if (_selectedUnit == null)
        {
            return;
        }

        _showMoveRange = true;
        _mode = ControlMode.MoveTarget;
        _selectedTile = ClampSelectionAnchor(_selectedUnit.TilePosition, _selectedUnit.Size);
        _reachableAnchors = ComputeReachableAnchors(_selectedUnit, out _pathParents);
        UpdateSelectionVisual();
        UpdateMoveOverlay();
        UpdatePathLine();
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

        var tiles = new HashSet<Vector2I>();

        foreach (var anchor in _reachableAnchors)
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

    private void UpdatePathLine()
    {
        if (_pathLine == null)
        {
            return;
        }

        if (_mode != ControlMode.MoveTarget || !_showMoveRange || _selectedUnit == null)
        {
            _pathLine.Visible = false;
            return;
        }

        if (!_reachableAnchors.Contains(_selectedTile))
        {
            _pathLine.Visible = false;
            return;
        }

        var path = BuildPath(_selectedTile);
        if (path == null || path.Count == 0)
        {
            _pathLine.Visible = false;
            return;
        }

        DrawPathLine(path, _selectedUnit.Size);
    }

    private HashSet<Vector2I> ComputeReachableAnchors(UnitActor unit, out Dictionary<Vector2I, Vector2I> cameFrom)
    {
        var reachable = new HashSet<Vector2I>();
        cameFrom = new Dictionary<Vector2I, Vector2I>();
        var frontier = new List<(Vector2I pos, float cost)>();
        var costSoFar = new Dictionary<Vector2I, float>();

        var start = ClampAnchor(unit.TilePosition, unit.Size);
        frontier.Add((start, 0f));
        costSoFar[start] = 0f;
        reachable.Add(start);

        var directions = new (Vector2I dir, float cost)[]
        {
            (new Vector2I(-1, 0), 1f),
            (new Vector2I(1, 0), 1f),
            (new Vector2I(0, -1), 1f),
            (new Vector2I(0, 1), 1f),
            (new Vector2I(-1, -1), 1.5f),
            (new Vector2I(1, -1), 1.5f),
            (new Vector2I(-1, 1), 1.5f),
            (new Vector2I(1, 1), 1.5f)
        };

        while (frontier.Count > 0)
        {
            frontier.Sort((a, b) => a.cost.CompareTo(b.cost));
            var current = frontier[0];
            frontier.RemoveAt(0);

            if (current.cost > unit.MovePoints)
            {
                continue;
            }

            foreach (var entry in directions)
            {
                var next = current.pos + entry.dir;
                if (!IsAnchorValid(next, unit))
                {
                    continue;
                }

                var nextCost = current.cost + entry.cost;
                if (nextCost > unit.MovePoints)
                {
                    continue;
                }

                if (costSoFar.TryGetValue(next, out var knownCost) && knownCost <= nextCost)
                {
                    continue;
                }

                costSoFar[next] = nextCost;
                reachable.Add(next);
                cameFrom[next] = current.pos;
                frontier.Add((next, nextCost));
            }
        }

        return reachable;
    }

    private List<Vector2I> BuildPath(Vector2I target)
    {
        if (_selectedUnit == null)
        {
            return null;
        }

        var path = new List<Vector2I>();
        var current = target;
        path.Add(current);

        while (_pathParents.TryGetValue(current, out var prev))
        {
            current = prev;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private void DrawPathLine(List<Vector2I> path, Vector2I size)
    {
        if (_pathLine == null)
        {
            return;
        }

        var mesh = _pathLine.Mesh as ImmediateMesh;
        if (mesh == null)
        {
            mesh = new ImmediateMesh();
            _pathLine.Mesh = mesh;
        }

        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
        foreach (var anchor in path)
        {
            var center = AnchorToWorld(anchor, size);
            var point = new Vector3(center.X, _origin.Y + OverlayHeight + 0.08f, center.Z);
            mesh.SurfaceAddVertex(point);
        }
        mesh.SurfaceEnd();
        _pathLine.Visible = true;
    }

    private async void MoveUnitAlongPath(UnitActor unit, List<Vector2I> path)
    {
        if (path == null || path.Count == 0)
        {
            return;
        }

        _isMoving = true;
        _showMoveRange = false;
        _mode = ControlMode.CommandSelect;

        if (_moveOverlay != null)
        {
            _moveOverlay.Visible = false;
        }
        if (_pathLine != null)
        {
            _pathLine.Visible = false;
        }

        if (path.Count == 1)
        {
            unit.TilePosition = path[0];
            SnapUnitToGrid(unit);
            RebuildOccupancy();
            unit.PlayIdle();
            UpdateSelectionAfterMove(unit);
            _isMoving = false;
            return;
        }

        unit.PlayMove();
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);

        for (int i = 1; i < path.Count; i++)
        {
            var center = AnchorToWorld(path[i], unit.Size);
            var target = new Vector3(center.X, _origin.Y, center.Z);
            tween.TweenProperty(unit, "global_position", target, MoveSecondsPerTile);
        }

        await ToSignal(tween, Tween.SignalName.Finished);
        unit.TilePosition = path[^1];
        RebuildOccupancy();
        unit.PlayIdle();
        UpdateSelectionAfterMove(unit);
        _isMoving = false;
    }

    private void UpdateSelectionAfterMove(UnitActor unit)
    {
        _selectedTile = ClampSelectionAnchor(unit.TilePosition, unit.Size);
        UpdateSelectionVisual();
        if (_commandPanel != null)
        {
            _commandPanel.SetSelection(_commandIndex);
        }
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

            if (!IsTileOnFloor(tile))
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

    private Vector2I GetSelectionSize()
    {
        var size = _selectedUnit != null && _mode != ControlMode.SelectTile ? _selectedUnit.Size : DefaultSelectionSize;
        if (size.X <= 0 || size.Y <= 0)
        {
            return new Vector2I(1, 1);
        }

        return size;
    }

    private Vector2I ClampSelectionAnchor(Vector2I anchor, Vector2I size)
    {
        return ClampAnchor(anchor, size);
    }

    private bool IsTileOnFloor(Vector2I tile)
    {
        if (_gridMap == null)
        {
            return true;
        }

        if (!IsWithinGrid(tile))
        {
            return false;
        }

        var cellX = tile.X / Mathf.Max(1, TilesPerFloorCell);
        var cellY = tile.Y / Mathf.Max(1, TilesPerFloorCell);
        if (cellX < 0 || cellY < 0 || cellX >= _dungeon.GridSize.X || cellY >= _dungeon.GridSize.Y)
        {
            return false;
        }

        return _gridMap.GetCellItem(new Vector3I(cellX, 0, cellY)) != -1;
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

    private MeshInstance3D CreatePathLine(string name, Color color)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        var instance = new MeshInstance3D
        {
            Name = name,
            Mesh = new ImmediateMesh(),
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
