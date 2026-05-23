using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.ConfigFlags;
using static Raylib_cs.Raylib;
using static Raylib_cs.TraceLogLevel;
using static rlImGui_cs.rlImGui;

internal static partial class Gouge {

    private static readonly Map Map = new();
    private static readonly string[] TextureFiles = Resources.GetResourceFiles("texture");
    private static readonly List<Map> History = [];
    
    private static bool _mode3D;
    
    private const float VertexSelectPixels = 10f;
    private const float LineSelectPixels = 8f;
    
    private static int _activePart = -1;
    private static (int part, Vector2 mouseStart, List<Vector2> vertices)? _selectedPart;
    private static ((int part, int start, int end) edge, Vector2 mouseStart, Vector2 startVertex, Vector2 endVertex)? _selectedLine;
    private static bool _selectedLineExtrudePending;
    private static (int part, int vertex) _selectedVertex = (-1, -1);
    private static bool _selectedVertexExtrudePending;
    private static readonly HashSet<(int part, int vertex)> SelectedVertices = [];
    private static List<((int part, int vertex) vertex, Vector2 start)>? _selectedVertexDragGroup;
    
    private static Vector2? _rectStart;
    private static int _rectParentPart = -1;
    private static Vector2? _selectionRectStartScreen;

    private static ImGuiIOPtr _io;
    private static Vector2 _mouseWorldPos;
    private static Vector2 _rightMousePressPos;
    private static bool _rightMouseDragged;
    private static bool _rightMouseUsedForCamera;
    private static (int part, int vertex, Vector2 screenStart, bool extrude)? _pendingVertex3D;
    private static ((int part, int start, int end, Vector2 point) line, Vector2 screenStart, bool extrude)? _pendingLine3D;
    private static (int part, Vector2 screenStart)? _pendingPart3D;

    private const float DragSpeed = .05f;
    private const int HistoryCapacity = 300;
    private static int _historyIndex = -1;
    
    public static unsafe void Main() {
        
        SetTraceLogLevel(Error);
        SetConfigFlags(ResizableWindow);
        InitWindow(1280, 720, "Gouge");
        SetWindowMonitor(0);
        SetExitKey(0);
        
        Setup();
        _io = ImGui.GetIO();
        _io.NativePtr->IniFilename = null;
        _io.Fonts.Clear();
        var montserratRegular = Resources.FindResourceFile("font", "Montserrat-Regular.ttf");
        _io.Fonts.AddFontFromFileTTF(montserratRegular, 18f);
        ReloadFonts();
        
        CreateDefaultPart();
        RecordHistorySnapshot();
        
        while (!WindowShouldClose()) {

            if (IsKeyPressed(KeyboardKey.Space))
                _mode3D = !_mode3D;

            HandleHistoryShortcuts();

            if (_mode3D) {
                if (!TryUpdateMouseWorldPos3D())
                    _mouseWorldPos = Render.Cam2D.Target;
            }
            else _mouseWorldPos = GetScreenToWorld2D(GetMousePosition(), Render.Cam2D);

            if (_mode3D)
                ProcessPending3DDrag();

            if (_mode3D)
                Handle3DPartWheel();

            MoveSelectedVertex();
            MoveSelectedLine();
            MoveSelectedPart();
            
            UpdateCamera();
            HandleActivePartKeyboardEdit();
            
            BeginDrawing();
            ClearBackground(Colors.Background);
            
            if (_mode3D) {

                var visibleHit = !_selectedLine.HasValue && !_selectedPart.HasValue
                    ? FindHoveredPartHit3D()
                    : (part: -1, distance: float.PositiveInfinity);
                var visiblePart = FindPreferredPartOnHoverPlane3D(visibleHit.part);

                var hoveredVertex = (-1, -1);
                var hoveredLine = (part: -1, start: -1, end: -1, point: Vector2.Zero);

                if (_selectedVertex == (-1, -1) && !_selectedLine.HasValue)
                    ResolveHoveredGeometry3D(visiblePart, visibleHit.distance, out hoveredVertex, out hoveredLine);

                var hoveredPart = hoveredVertex == (-1, -1) && hoveredLine.part == -1 && _selectedVertex == (-1, -1) && !_selectedLine.HasValue && !_selectedPart.HasValue
                    ? visiblePart
                    : -1;

                Vector2? previewVertex = null;

                Handle2DEditing(ref hoveredVertex, ref hoveredLine, ref hoveredPart, ref previewVertex);

                BeginMode3D(Render.Cam3D);
            
                DrawGrid3D();
                Render.Map(Map);
                DrawParts3D(hoveredVertex, hoveredLine, hoveredPart);
                DrawRectPreview3D();

                if (previewVertex.HasValue) {
                    var y = hoveredLine.part != -1
                        ? Map.Parts[hoveredLine.part].YOffset
                        : GetEditPlaneY();
                    DrawCubeV(new Vector3(previewVertex.Value.X, y, previewVertex.Value.Y), new Vector3(HandleSize3D), Colors.Orange);
                }
            
                EndMode3D();
                
            } else {
                
                Render.Cam2D.Offset = new Vector2(GetScreenWidth() * 0.5f, GetScreenHeight() * 0.5f);
                
                BeginMode2D(Render.Cam2D);

                DrawGrid();

                var visiblePart = !_selectedLine.HasValue && !_selectedPart.HasValue
                    ? Map.FindPartContaining(_mouseWorldPos)
                    : -1;

                var hoveredVertex = _selectedVertex == (-1, -1)
                    ? FindHoveredVertex(visiblePart)
                    : (-1, -1);
                var hoveredLine = hoveredVertex == (-1, -1) && _selectedVertex == (-1, -1) && !_selectedLine.HasValue
                    ? FindHoveredLine(visiblePart)
                    : (part: -1, start: -1, end: -1, point: Vector2.Zero);
                var hoveredPart = hoveredVertex == (-1, -1) && hoveredLine.part == -1 && _selectedVertex == (-1, -1) && !_selectedLine.HasValue && !_selectedPart.HasValue
                    ? visiblePart
                    : -1;

                Vector2? previewVertex = null;

                Handle2DEditing(ref hoveredVertex, ref hoveredLine, ref hoveredPart, ref previewVertex);

                DrawParts(hoveredVertex, hoveredLine, hoveredPart);
                DrawRectPreview();

                if (previewVertex.HasValue)
                    Render.Square(Colors.Orange, previewVertex.Value, .1f);

                EndMode2D();
            }

            DrawSelectionRectOverlay();

            Begin();
        
            DrawPartInspector();
        
            End();
        
            EndDrawing();
        }

        Shutdown();
        
        CloseWindow();
    }

    private static void CreateDefaultPart() {
        Map.Parts.Add(CreateRectanglePart(new Vector2(-5f, -5f), new Vector2(5f, 5f)));
        _activePart = 0;
    }

    private static void HandleHistoryShortcuts() {

        if (_io.WantTextInput)
            return;

        var ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);

        if (!ctrl)
            return;

        var shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);

        if (shift && IsHistoryShortcutPressed(KeyboardKey.Z) || IsHistoryShortcutPressed(KeyboardKey.Y)) {
            RedoHistory();
            return;
        }

        if (IsHistoryShortcutPressed(KeyboardKey.Z))
            UndoHistory();
    }

    private static bool IsHistoryShortcutPressed(KeyboardKey key) =>
        IsKeyPressed(key) || IsKeyPressedRepeat(key);

    private static void HandleActivePartKeyboardEdit() {

        if (_io.WantCaptureKeyboard)
            return;

        if (_selectedVertex != (-1, -1) || _selectedLine.HasValue || _selectedPart.HasValue || _rectStart.HasValue || HasPending3DDrag())
            return;

        if (SelectedVertices.Count > 0) {
            HandleSelectedVerticesKeyboardEdit();
            return;
        }

        var ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);

        if (_activePart < 0 || _activePart >= Map.Parts.Count)
            return;

        if (ctrl && IsKeyPressed(KeyboardKey.D)) {
            DuplicateActivePart();
            return;
        }

        if (IsKeyPressed(KeyboardKey.Delete)) {
            DeleteActivePart();
            return;
        }

        var step = IsKeyDown(KeyboardKey.LeftAlt) || IsKeyDown(KeyboardKey.RightAlt) ? 0.1f : 1f;
        var shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);
        var moved = shift
            ? (_mode3D ? TryEditActivePartVertices3D(step, ctrl) : TryEditActivePartVertices2D(step, ctrl))
            : (_mode3D ? TryMoveActivePart3D(step) : TryMoveActivePart2D(step));

        if (moved)
            RecordHistorySnapshot();
    }

    private static void HandleSelectedVerticesKeyboardEdit() {

        if (IsKeyPressed(KeyboardKey.Delete)) {
            DeleteSelectedVertices();
            return;
        }

        var step = IsKeyDown(KeyboardKey.LeftAlt) || IsKeyDown(KeyboardKey.RightAlt) ? 0.1f : 1f;
        var moved = _mode3D
            ? TryMoveSelectedVertices3D(step)
            : TryMoveSelectedVertices2D(step);

        if (moved)
            RecordHistorySnapshot();
    }

    private static bool TryMoveActivePart2D(float step) {

        var keyMove = GetArrowKeyDirection();

        if (keyMove == Vector2.Zero)
            return false;

        var pivot = GetActivePartCenter2D(_activePart);
        var right = GetScreenAlignedAxis2D(pivot, new Vector2(1f, 0f));
        var up = GetScreenAlignedAxis2D(pivot, new Vector2(0f, -1f));
        var planarMove = right * keyMove.X + up * keyMove.Y;

        if (planarMove == Vector2.Zero)
            return false;

        TranslatePart(_activePart, planarMove * step, 0f);
        return true;
    }

    private static bool TryMoveSelectedVertices2D(float step) {

        var keyMove = GetArrowKeyDirection();

        if (keyMove == Vector2.Zero)
            return false;

        var pivot = GetSelectedVerticesCenter2D();
        var right = GetScreenAlignedAxis2D(pivot, new Vector2(1f, 0f));
        var up = GetScreenAlignedAxis2D(pivot, new Vector2(0f, -1f));
        var planarMove = right * keyMove.X + up * keyMove.Y;

        if (planarMove == Vector2.Zero)
            return false;

        TranslateSelectedVertices(planarMove * step);
        return true;
    }

    private static bool TryMoveActivePart3D(float step) {

        var keyMove = GetArrowKeyDirection();

        if (keyMove == Vector2.Zero)
            return false;

        var pivot = GetActivePartCenter(_activePart);
        var right = GetScreenAlignedAxis3D(pivot, new Vector2(1f, 0f));
        var up = GetScreenAlignedAxis3D(pivot, new Vector2(0f, -1f));
        var worldMove = right * keyMove.X + up * keyMove.Y;

        if (worldMove == Vector3.Zero)
            return false;

        TranslatePart(_activePart, new Vector2(worldMove.X, worldMove.Z) * step, worldMove.Y * step);
        return true;
    }

    private static bool TryMoveSelectedVertices3D(float step) {

        var keyMove = GetArrowKeyDirection();

        if (keyMove == Vector2.Zero)
            return false;

        var forward = GetSnappedPlanarForwardAxis3D();
        var right = GetSnappedPlanarRightAxis3D(forward);
        var direction = right * keyMove.X + forward * keyMove.Y;

        if (direction == Vector2.Zero)
            return false;

        TranslateSelectedVertices(Vector2.Normalize(direction) * step);
        return true;
    }

    private static bool TryEditActivePartVertices2D(float step, bool extrude) {

        var keyMove = GetArrowKeyDirection();

        if (keyMove == Vector2.Zero)
            return false;

        var pivot = GetActivePartCenter2D(_activePart);
        var right = GetScreenAlignedAxis2D(pivot, new Vector2(1f, 0f));
        var up = GetScreenAlignedAxis2D(pivot, new Vector2(0f, -1f));
        var direction = right * keyMove.X + up * keyMove.Y;

        if (direction == Vector2.Zero)
            return false;

        return extrude
            ? ExtrudeExtremeVertices(_activePart, Vector2.Normalize(direction), step)
            : MoveExtremeVertices(_activePart, Vector2.Normalize(direction), step);
    }

    private static bool TryEditActivePartVertices3D(float step, bool extrude) {

        var keyMove = GetArrowKeyDirection();

        if (keyMove == Vector2.Zero)
            return false;

        var forward = GetSnappedPlanarForwardAxis3D();
        var right = GetSnappedPlanarRightAxis3D(forward);
        var direction = right * keyMove.X + forward * keyMove.Y;

        if (direction == Vector2.Zero)
            return false;

        return extrude
            ? ExtrudeExtremeVertices(_activePart, Vector2.Normalize(direction), step)
            : MoveExtremeVertices(_activePart, Vector2.Normalize(direction), step);
    }

    private static Vector2 GetActivePartCenter2D(int partIndex) {

        var part = Map.Parts[partIndex];
        var sum = Vector2.Zero;

        foreach (var vertex in part.Vertices)
            sum += vertex;

        return sum / part.Vertices.Count;
    }

    private static Vector2 GetSelectedVerticesCenter2D() {

        var sum = Vector2.Zero;

        foreach (var (part, vertex) in SelectedVertices)
            sum += Map.Parts[part].Vertices[vertex];

        return sum / SelectedVertices.Count;
    }

    private static Vector2 GetSnappedPlanarForwardAxis3D() {

        var forward3D = Vector3.Normalize(Render.Cam3D.Target - Render.Cam3D.Position);
        var forward = new Vector2(forward3D.X, forward3D.Z);

        if (forward == Vector2.Zero)
            return Vector2.UnitY;

        if (MathF.Abs(forward.X) >= MathF.Abs(forward.Y))
            return forward.X >= 0f ? Vector2.UnitX : -Vector2.UnitX;

        return forward.Y >= 0f ? Vector2.UnitY : -Vector2.UnitY;
    }

    private static Vector2 GetSnappedPlanarRightAxis3D(Vector2 forward) =>
        new(-forward.Y, forward.X);

    private static Vector3 GetActivePartCenter(int partIndex) {

        var part = Map.Parts[partIndex];
        var sum = Vector2.Zero;

        foreach (var vertex in part.Vertices)
            sum += vertex;

        var planar = sum / part.Vertices.Count;
        return new Vector3(planar.X, part.YOffset + part.Height * 0.5f, planar.Y);
    }

    private static Vector2 GetScreenAlignedAxis2D(Vector2 pivot, Vector2 desiredScreenDirection) {

        var pivotScreen = GetWorldToScreen2D(pivot, Render.Cam2D);
        var candidates = new[] {
            Vector2.UnitX,
            -Vector2.UnitX,
            Vector2.UnitY,
            -Vector2.UnitY,
        };

        var bestAxis = Vector2.Zero;
        var bestScore = float.NegativeInfinity;

        foreach (var axis in candidates) {
            var candidateScreen = GetWorldToScreen2D(pivot + axis, Render.Cam2D);
            var screenDelta = candidateScreen - pivotScreen;

            if (screenDelta == Vector2.Zero)
                continue;

            var score = Vector2.Dot(Vector2.Normalize(screenDelta), desiredScreenDirection);

            if (score > bestScore) {
                bestScore = score;
                bestAxis = axis;
            }
        }

        return bestAxis;
    }

    private static Vector3 GetScreenAlignedAxis3D(Vector3 pivot, Vector2 desiredScreenDirection) {

        var pivotScreen = GetWorldToScreen(pivot, Render.Cam3D);
        var candidates = new[] {
            Vector3.UnitX,
            -Vector3.UnitX,
            Vector3.UnitY,
            -Vector3.UnitY,
            Vector3.UnitZ,
            -Vector3.UnitZ,
        };

        var bestAxis = Vector3.Zero;
        var bestScore = float.NegativeInfinity;

        foreach (var axis in candidates) {
            var candidateScreen = GetWorldToScreen(pivot + axis, Render.Cam3D);
            var screenDelta = candidateScreen - pivotScreen;

            if (screenDelta == Vector2.Zero)
                continue;

            var score = Vector2.Dot(Vector2.Normalize(screenDelta), desiredScreenDirection);

            if (score > bestScore) {
                bestScore = score;
                bestAxis = axis;
            }
        }

        return bestAxis;
    }

    private static Vector2 GetArrowKeyDirection() {

        var direction = Vector2.Zero;

        if (IsKeyPressed(KeyboardKey.Left) || IsKeyPressedRepeat(KeyboardKey.Left))
            direction.X--;

        if (IsKeyPressed(KeyboardKey.Right) || IsKeyPressedRepeat(KeyboardKey.Right))
            direction.X++;

        if (IsKeyPressed(KeyboardKey.Up) || IsKeyPressedRepeat(KeyboardKey.Up))
            direction.Y++;

        if (IsKeyPressed(KeyboardKey.Down) || IsKeyPressedRepeat(KeyboardKey.Down))
            direction.Y--;

        return direction == Vector2.Zero ? direction : Vector2.Normalize(direction);
    }

    private static bool MoveExtremeVertices(int partIndex, Vector2 direction, float step) {

        var part = Map.Parts[partIndex];
        var extreme = GetExtremeVertexIndices(part.Vertices, direction);

        if (extreme.Count == 0)
            return false;

        var delta = direction * step;

        foreach (var index in extreme)
            part.Vertices[index] += delta;

        return true;
    }

    private static bool ExtrudeExtremeVertices(int partIndex, Vector2 direction, float step) {

        var part = Map.Parts[partIndex];
        var extreme = GetExtremeVertexIndices(part.Vertices, direction);

        if (extreme.Count == 0 || extreme.Count == part.Vertices.Count)
            return MoveExtremeVertices(partIndex, direction, step);

        var vertices = RotateVerticesToNonExtremeStart(part.Vertices, extreme);
        var selected = vertices.Select(vertex => IsExtremeVertex(vertex, vertices, direction)).ToArray();
        var delta = direction * step;
        var extruded = new List<Vector2>(vertices.Count + extreme.Count + 2);

        for (var i = 0; i < vertices.Count; i++) {
            extruded.Add(vertices[i]);

            if (!selected[i] || selected[(i - 1 + selected.Length) % selected.Length])
                continue;

            var end = i;

            while (end + 1 < selected.Length && selected[end + 1])
                end++;

            for (var j = i; j <= end; j++)
                extruded.Add(vertices[j] + delta);

            extruded.Add(vertices[end]);
            i = end;
        }

        part.Vertices.Clear();
        part.Vertices.AddRange(extruded);
        return true;
    }

    private static List<int> GetExtremeVertexIndices(List<Vector2> vertices, Vector2 direction) {

        var maxProjection = float.NegativeInfinity;

        foreach (var vertex in vertices)
            maxProjection = MathF.Max(maxProjection, Vector2.Dot(vertex, direction));

        var indices = new List<int>();

        for (var i = 0; i < vertices.Count; i++) {
            if (MathF.Abs(Vector2.Dot(vertices[i], direction) - maxProjection) <= 0.0001f)
                indices.Add(i);
        }

        return indices;
    }

    private static bool IsExtremeVertex(Vector2 vertex, List<Vector2> vertices, Vector2 direction) {

        var projection = Vector2.Dot(vertex, direction);
        var maxProjection = float.NegativeInfinity;

        foreach (var candidate in vertices)
            maxProjection = MathF.Max(maxProjection, Vector2.Dot(candidate, direction));

        return MathF.Abs(projection - maxProjection) <= 0.0001f;
    }

    private static List<Vector2> RotateVerticesToNonExtremeStart(List<Vector2> vertices, List<int> extreme) {

        var selected = new HashSet<int>(extreme);
        var start = Enumerable.Range(0, vertices.Count).First(index => !selected.Contains(index));
        var rotated = new List<Vector2>(vertices.Count);

        for (var i = 0; i < vertices.Count; i++)
            rotated.Add(vertices[(start + i) % vertices.Count]);

        return rotated;
    }

    private static void TranslatePart(int partIndex, Vector2 planarDelta, float yDelta) {

        var part = Map.Parts[partIndex];

        for (var i = 0; i < part.Vertices.Count; i++)
            part.Vertices[i] += planarDelta;

        part.YOffset += yDelta;
    }

    private static void TranslateSelectedVertices(Vector2 delta) {

        foreach (var (part, vertex) in SelectedVertices)
            Map.Parts[part].Vertices[vertex] += delta;
    }

    private static void DeleteActivePart() {

        Map.Parts.RemoveAt(_activePart);
        ClearActiveEditState();

        if (Map.Parts.Count == 0)
            _activePart = -1;
        else if (_activePart >= Map.Parts.Count)
            _activePart = Map.Parts.Count - 1;

        RecordHistorySnapshot();
    }

    private static void DuplicateActivePart() {

        var clone = Map.Parts[_activePart].Clone();
        Map.Parts.Add(clone);
        ClearActiveEditState();
        _activePart = Map.Parts.Count - 1;
        RecordHistorySnapshot();
    }

    private static void DeleteSelectedVertices() {

        var removedActivePart = false;
        var removedBeforeActivePart = 0;

        foreach (var group in SelectedVertices.GroupBy(selection => selection.part).OrderByDescending(group => group.Key)) {
            var partIndex = group.Key;

            if (partIndex < 0 || partIndex >= Map.Parts.Count)
                continue;

            var part = Map.Parts[partIndex];
            var indices = group.Select(selection => selection.vertex).Distinct().OrderByDescending(index => index).ToArray();

            if (part.Vertices.Count - indices.Length < 3) {
                Map.Parts.RemoveAt(partIndex);
                removedActivePart |= _activePart == partIndex;
                if (partIndex < _activePart)
                    removedBeforeActivePart++;
                continue;
            }

            foreach (var index in indices)
                part.Vertices.RemoveAt(index);
        }

        SelectedVertices.Clear();
        _selectedVertex = (-1, -1);
        _selectedVertexDragGroup = null;

        if (Map.Parts.Count == 0)
            _activePart = -1;
        else if (removedActivePart)
            _activePart = Math.Clamp(_activePart - removedBeforeActivePart, 0, Map.Parts.Count - 1);
        else if (_activePart >= Map.Parts.Count)
            _activePart = Math.Clamp(_activePart, 0, Map.Parts.Count - 1);
        else _activePart = Math.Clamp(_activePart - removedBeforeActivePart, 0, Map.Parts.Count - 1);

        RecordHistorySnapshot();
    }

    private static void StartVertexSelectionRect() =>
        _selectionRectStartScreen = GetMousePosition();

    private static bool HasSelectionRect() => _selectionRectStartScreen.HasValue;

    private static void FinishVertexSelectionRect() {

        if (!_selectionRectStartScreen.HasValue)
            return;

        var rect = GetSelectionRectScreen();
        _selectionRectStartScreen = null;
        SelectedVertices.Clear();

        if (rect.Width <= float.Epsilon || rect.Height <= float.Epsilon)
            return;

        for (var i = 0; i < Map.Parts.Count; i++) {
            var part = Map.Parts[i];

            for (var j = 0; j < part.Vertices.Count; j++) {
                var screen = GetVertexScreenPosition(i, j);

                if (CheckCollisionPointRec(screen, rect))
                    SelectedVertices.Add((i, j));
            }
        }
    }

    private static Rectangle GetSelectionRectScreen() {

        var start = _selectionRectStartScreen ?? GetMousePosition();
        var end = GetMousePosition();
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);
        return new Rectangle(min.X, min.Y, max.X - min.X, max.Y - min.Y);
    }

    private static Vector2 GetVertexScreenPosition(int partIndex, int vertexIndex) {

        var part = Map.Parts[partIndex];
        var vertex = part.Vertices[vertexIndex];

        return _mode3D
            ? GetWorldToScreen(new Vector3(vertex.X, part.YOffset, vertex.Y), Render.Cam3D)
            : GetWorldToScreen2D(vertex, Render.Cam2D);
    }

    private static void DrawSelectionRectOverlay() {

        if (!_selectionRectStartScreen.HasValue)
            return;

        var rect = GetSelectionRectScreen();
        DrawRectangleRec(rect, new Color(255, 165, 0, 28));
        DrawRectangleLinesEx(rect, 1f, Colors.Orange);
    }

    private static void BeginVertexDrag((int part, int vertex) vertex, bool extrude) {
        if (SelectedVertices.Contains(vertex)) {
            _selectedVertexDragGroup = SelectedVertices
                .OrderBy(selection => selection.part)
                .ThenBy(selection => selection.vertex)
                .Select(selection => (selection, Map.Parts[selection.part].Vertices[selection.vertex]))
                .ToList();
            _selectedVertexExtrudePending = false;
        }
        else {
            _selectedVertexDragGroup = null;
            SelectedVertices.Clear();
            _selectedVertexExtrudePending = extrude;
        }

        _selectedVertex = vertex;
        _activePart = vertex.part;
    }

    private static void BeginLineDrag(int part, int start, int end, Vector2 mouseStart, Vector2 startVertex, Vector2 endVertex, bool extrude) {
        _selectedVertexDragGroup = null;
        SelectedVertices.Clear();
        _selectedLine = (
            (part, start, end),
            mouseStart,
            startVertex,
            endVertex
        );
        _selectedLineExtrudePending = extrude;
        _activePart = part;
    }

    private static bool IsCtrlDown() =>
        IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);

    private static void ExtrudeSelectedVertex() {

        var selection = _selectedVertex;
        var vertices = Map.Parts[selection.part].Vertices;
        var insertIndex = selection.vertex + 1;
        vertices.Insert(insertIndex, vertices[selection.vertex]);
        _selectedVertex = (selection.part, insertIndex);
        _selectedVertexExtrudePending = false;
    }

    private static void ExtrudeSelectedLine() {

        if (!_selectedLine.HasValue)
            return;

        var selection = _selectedLine.Value;
        var (start, end) = NormalizeLineExtrudeEdge(selection.edge.part, selection.edge.start, selection.edge.end);
        var vertices = Map.Parts[selection.edge.part].Vertices;
        var startVertex = vertices[start];
        var endVertex = vertices[end];

        vertices.Insert(start + 1, startVertex);
        vertices.Insert(start + 2, endVertex);

        _selectedLine = (
            (selection.edge.part, start + 1, start + 2),
            selection.mouseStart,
            startVertex,
            endVertex
        );
        _selectedLineExtrudePending = false;
    }

    private static (int start, int end) NormalizeLineExtrudeEdge(int partIndex, int start, int end) {

        var vertices = Map.Parts[partIndex].Vertices;

        if (end == start + 1)
            return (start, end);

        if (!(start == vertices.Count - 1 && end == 0))
            return (start, end);

        var rotated = new List<Vector2>(vertices.Count);

        for (var i = 0; i < vertices.Count; i++)
            rotated.Add(vertices[(start + i) % vertices.Count]);

        vertices.Clear();
        vertices.AddRange(rotated);
        return (0, 1);
    }

    private static void RecordHistorySnapshot() {

        var snapshot = Map.Clone();

        if (_historyIndex >= 0 && History[_historyIndex].ContentEquals(snapshot))
            return;

        if (_historyIndex < History.Count - 1)
            History.RemoveRange(_historyIndex + 1, History.Count - _historyIndex - 1);

        History.Add(snapshot);

        if (History.Count > HistoryCapacity)
            History.RemoveAt(0);

        _historyIndex = History.Count - 1;
    }

    private static void UndoHistory() {

        if (_historyIndex <= 0)
            return;

        _historyIndex--;
        ApplyHistorySnapshot(History[_historyIndex]);
    }

    private static void RedoHistory() {

        if (_historyIndex >= History.Count - 1)
            return;

        _historyIndex++;
        ApplyHistorySnapshot(History[_historyIndex]);
    }

    private static void ApplyHistorySnapshot(Map snapshot) {
        Map.CopyFrom(snapshot);
        ClearActiveEditState();

        if (Map.Parts.Count == 0)
            _activePart = -1;
        else if (_activePart >= Map.Parts.Count)
            _activePart = Map.Parts.Count - 1;
    }

    private static void ClearActiveEditState() {
        _selectedPart = null;
        _selectedLine = null;
        _selectedLineExtrudePending = false;
        _selectedVertex = (-1, -1);
        _selectedVertexExtrudePending = false;
        _selectedVertexDragGroup = null;
        SelectedVertices.Clear();
        _rectStart = null;
        _rectParentPart = -1;
        _selectionRectStartScreen = null;
        _pendingVertex3D = null;
        _pendingLine3D = null;
        _pendingPart3D = null;
    }

    private static void MoveSelectedVertex() {
        
        if (_selectedVertex == (-1, -1)) return;

        var snappedPos = Snap(_mouseWorldPos);

        if (_selectedVertexDragGroup is { Count: > 0 }) {
            var anchor = _selectedVertexDragGroup.First(entry => entry.vertex == _selectedVertex).start;
            var delta = snappedPos - anchor;

            foreach (var entry in _selectedVertexDragGroup)
                Map.Parts[entry.vertex.part].Vertices[entry.vertex.vertex] = entry.start + delta;
        }
        else {
            if (_selectedVertexExtrudePending && snappedPos != Map.Parts[_selectedVertex.part].Vertices[_selectedVertex.vertex])
                ExtrudeSelectedVertex();

            Map.Parts[_selectedVertex.part].Vertices[_selectedVertex.vertex] = snappedPos;
        }

        if (!IsMouseButtonUp(MouseButton.Left)) return;

        if (_selectedVertexDragGroup is null)
            TryMergeVertex(snappedPos);
        
        _selectedVertex = (-1, -1);
        _selectedVertexExtrudePending = false;
        _selectedVertexDragGroup = null;
        RecordHistorySnapshot();
    }

    private static void MoveSelectedLine() {

        if (!_selectedLine.HasValue) return;

        var selection = _selectedLine.Value;
        var delta = Snap(_mouseWorldPos) - selection.mouseStart;

        if (_selectedLineExtrudePending && delta != Vector2.Zero) {
            ExtrudeSelectedLine();
            selection = _selectedLine!.Value;
            delta = Snap(_mouseWorldPos) - selection.mouseStart;
        }

        var vertices = Map.Parts[selection.edge.part].Vertices;

        vertices[selection.edge.start] = selection.startVertex + delta;
        vertices[selection.edge.end] = selection.endVertex + delta;

        if (!IsMouseButtonUp(MouseButton.Left)) return;

        _selectedLine = null;
        _selectedLineExtrudePending = false;
        RecordHistorySnapshot();
    }

    private static void MoveSelectedPart() {

        if (!_selectedPart.HasValue) return;

        var selection = _selectedPart.Value;
        var delta = Snap(_mouseWorldPos) - selection.mouseStart;
        var vertices = Map.Parts[selection.part].Vertices;

        for (var i = 0; i < vertices.Count; i++)
            vertices[i] = selection.vertices[i] + delta;

        if (!IsMouseButtonUp(MouseButton.Left)) return;

        _selectedPart = null;
        RecordHistorySnapshot();
    }

    private static Vector2 Snap(Vector2 position) {
        
        var snap = IsKeyDown(KeyboardKey.LeftAlt) ? 10f : 1f;

        return new Vector2 (
            
            MathF.Round(position.X * snap, MidpointRounding.AwayFromZero) / snap,
            MathF.Round(position.Y * snap, MidpointRounding.AwayFromZero) / snap
        );
    }
    
    private static void TryMergeVertex(Vector2 position) {

        var vertices = Map.Parts[_selectedVertex.part].Vertices;

        if (vertices.Where((_, i) => i != _selectedVertex.vertex).All(vertex => Vector2.DistanceSquared(vertex, position) > float.Epsilon * float.Epsilon))
            return;
        
        Map.DeleteVertex(_selectedVertex);
    }

    private static void Handle3DPartWheel() {

        if (_io.WantCaptureMouse || _activePart < 0 || _activePart >= Map.Parts.Count)
            return;

        var wheel = GetMouseWheelMove();

        if (wheel == 0f)
            return;

        var part = Map.Parts[_activePart];
        var step = IsKeyDown(KeyboardKey.LeftAlt) || IsKeyDown(KeyboardKey.RightAlt) ? 0.125f : 1f;
        var delta = wheel * step;

        if (IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift))
            part.YOffset += delta;
        else part.Height = MathF.Max(0.25f, part.Height + delta);

        RecordHistorySnapshot();
    }

    private static void DrawGrid() {
        
        if (IsKeyDown(KeyboardKey.LeftAlt)) {
            
            Render.Grid(.1f, Colors.GridBottom);
            Render.Grid(1f, Colors.GridTop);
        }
        else Render.Grid(1f, Colors.GridBottom);
    }

    private static Part CreateRectanglePart(Vector2 min, Vector2 max) {

        var part = new Part();
        part.Vertices.AddRange(Geometry2D.CreateRectangleVertices(min, max));
        return part;
    }
}
