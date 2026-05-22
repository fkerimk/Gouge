using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using static Raylib_cs.ConfigFlags;
using static Raylib_cs.Raylib;
using static Raylib_cs.TraceLogLevel;
using static rlImGui_cs.rlImGui;

internal static partial class Gouge {

    private static readonly Map Map = new();
    private static readonly string[] TextureFiles = Resources.GetResourceFiles("texture");
    
    private static bool _mode3D;
    
    private const float VertexSelectPixels = 10f;
    private const float LineSelectPixels = 8f;
    
    private static int _activePart = -1;
    private static int _hoveredPart3D = -1;
    private static (int part, Vector2 mouseStart, List<Vector2> vertices)? _selectedPart;
    private static ((int part, int start, int end) edge, Vector2 mouseStart, Vector2 startVertex, Vector2 endVertex)? _selectedLine;
    private static (int part, int vertex) _selectedVertex = (-1, -1);
    
    private static Vector2? _rectStart;
    private static int _rectParentPart = -1;

    public static ImGuiIOPtr Io;
    public static Vector2 MouseWorldPos;
    
    public const float DragSpeed = .05f;
    
    public static unsafe void Main() {
        
        SetTraceLogLevel(Error);
        SetConfigFlags(ResizableWindow);
        InitWindow(1280, 720, "Gouge");
        SetWindowMonitor(0);
        SetExitKey(0);
        
        Setup();
        Io = ImGui.GetIO();
        Io.NativePtr->IniFilename = null;
        Io.Fonts.Clear();
        var montserratRegular = Resources.FindResourceFile("font", "Montserrat-Regular.ttf");
        Io.Fonts.AddFontFromFileTTF(montserratRegular, 18f);
        ReloadFonts();
        
        CreateDefaultPart();
        
        while (!WindowShouldClose()) {
            
            MouseWorldPos = GetScreenToWorld2D(GetMousePosition(), Render.Cam2D);

            if (IsKeyPressed(KeyboardKey.Space))
                _mode3D = !_mode3D;
            
            if (_mode3D) Handle3DSelection();
            
            else {
                
                MoveSelectedVertex();
                MoveSelectedLine();
                MoveSelectedPart();
            }
            
            UpdateCamera();
            
            BeginDrawing();
            ClearBackground(Colors.Background);
            
            if (_mode3D) {
                
                BeginMode3D(Render.Cam3D);
            
                Render.Map(Map);

                if (_hoveredPart3D != -1 && _hoveredPart3D < Map.Parts.Count && _hoveredPart3D != _activePart)
                    Render.PartOutline(Map.Parts[_hoveredPart3D], Colors.Gold);

                if (_activePart != -1 && _activePart < Map.Parts.Count)
                    Render.PartOutline(Map.Parts[_activePart], Colors.Orange);
            
                EndMode3D();
                
            } else {
                
                Render.Cam2D.Offset = new Vector2(GetScreenWidth() * 0.5f, GetScreenHeight() * 0.5f);
                
                BeginMode2D(Render.Cam2D);;

                DrawGrid();

                var hoveredVertex = _selectedVertex == (-1, -1)
                    ? FindHoveredVertex()
                    : (-1, -1);
                var hoveredLine = hoveredVertex == (-1, -1) && _selectedVertex == (-1, -1) && !_selectedLine.HasValue
                    ? FindHoveredLine()
                    : (part: -1, start: -1, end: -1, point: Vector2.Zero);
                var hoveredPart = hoveredVertex == (-1, -1) && hoveredLine.part == -1 && _selectedVertex == (-1, -1) && !_selectedLine.HasValue && !_selectedPart.HasValue
                    ? Map.FindPartContaining(MouseWorldPos)
                    : -1;

                Vector2? previewVertex = null;

                Handle2DEditing(ref hoveredVertex, ref hoveredLine, ref hoveredPart, ref previewVertex);

                DrawParts(hoveredVertex, hoveredLine, hoveredPart);
                DrawRectPreview();

                if (previewVertex.HasValue)
                    Render.Square(Colors.Orange, previewVertex.Value, .1f);

                EndMode2D();
            }

            Begin();
        
            DrawPartInspector();
        
            End();
        
            EndDrawing();
        }

        Shutdown();
        
        CloseWindow();
    }

    private static void CreateDefaultPart() {
        
        var part = new Part();

        part.Vertices.Add(new Vector2(-5, -5));
        part.Vertices.Add(new Vector2(-5,  5));
        part.Vertices.Add(new Vector2( 5,  5));
        part.Vertices.Add(new Vector2( 5, -5));

        Map.Parts.Add(part);
        _activePart = 0;
    }

    private static void MoveSelectedVertex() {
        
        if (_selectedVertex == (-1, -1)) return;

        var snappedPos = Snap(MouseWorldPos);

        Map.Parts[_selectedVertex.part].Vertices[_selectedVertex.vertex] = snappedPos;

        if (!IsMouseButtonUp(MouseButton.Left)) return;

        TryMergeVertex(snappedPos);
        
        _selectedVertex = (-1, -1);
    }

    private static void MoveSelectedLine() {

        if (!_selectedLine.HasValue) return;

        var selection = _selectedLine.Value;
        var delta = Snap(MouseWorldPos) - selection.mouseStart;
        var vertices = Map.Parts[selection.edge.part].Vertices;

        vertices[selection.edge.start] = selection.startVertex + delta;
        vertices[selection.edge.end] = selection.endVertex + delta;

        if (!IsMouseButtonUp(MouseButton.Left)) return;

        _selectedLine = null;
    }

    private static void MoveSelectedPart() {

        if (!_selectedPart.HasValue) return;

        var selection = _selectedPart.Value;
        var delta = Snap(MouseWorldPos) - selection.mouseStart;
        var vertices = Map.Parts[selection.part].Vertices;

        for (var i = 0; i < vertices.Count; i++)
            vertices[i] = selection.vertices[i] + delta;

        if (!IsMouseButtonUp(MouseButton.Left)) return;

        _selectedPart = null;
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

    private static void DrawGrid() {
        
        if (IsKeyDown(KeyboardKey.LeftAlt)) {
            
            Render.Grid(.1f, Colors.GridBottom);
            Render.Grid(1f, Colors.GridTop);
        }
        else Render.Grid(1f, Colors.GridBottom);
    }

    private static (int part, int vertex) FindHoveredVertex() {

        var bestDistance = GetVertexSelectDistance();
        var hovered = (-1, -1);

        for (var i = 0; i < Map.Parts.Count; i++) {
            
            var vertices = Map.Parts[i].Vertices;

            for (var j = 0; j < vertices.Count; j++) {

                var distance = Raymath.Vector2Distance(MouseWorldPos, vertices[j]);

                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                hovered = (i, j);
            }
        }

        return hovered;
    }

    private static (int part, int start, int end, Vector2 point) FindHoveredLine() {
        
        return !Map.TryFindLine(GetLineSelectDistance(), out var point, out var partIndex, out var startIndex, out var endIndex)
            ? (-1, -1, -1, Vector2.Zero)
            : (partIndex, startIndex, endIndex, point);
    }

    private static void SelectOrInsertVertex((int part, int vertex) hoveredVertex) {
        
        if (hoveredVertex != (-1, -1)) {
            
            _selectedVertex = hoveredVertex;
            _activePart = hoveredVertex.part;
            
            return;
        }

        if (!Map.TryFindPointOnLine(GetLineSelectDistance(), out var point, out var partIndex, out var insertIndex)) return;

        Map.InsertVertex(partIndex, insertIndex, point);
        
        _selectedVertex = (partIndex, insertIndex);
        _activePart = partIndex;
    }

    private static void DrawParts((int part, int vertex) hoveredVertex, (int part, int start, int end, Vector2 point) hoveredLine, int hoveredPart) {
        
        for (var i = 0; i < Map.Parts.Count; i++) {
            
            var part = Map.Parts[i];
            var vertices = part.Vertices;

            Render.Shape(vertices, GetPartColor(i, hoveredPart), Colors.PartFail);

            for (var j = 0; j < vertices.Count; j++) {
                
                var vertex = vertices[j];

                DrawEdge(vertices, j, hoveredLine.part == i && hoveredLine.start == j && hoveredLine.end == (j + 1) % vertices.Count);

                var color = _selectedVertex == (i, j) || hoveredVertex == (i, j)
                    ? Colors.Orange
                    : Colors.White;

                Render.Square(color, vertex, .1f);
            }
        }
    }

    private static Color GetPartColor(int partIndex, int hoveredPart) {
        
        if (_activePart != partIndex) return hoveredPart == partIndex ? Colors.PartHover : Colors.Part;
        
        var pulse = (MathF.Sin((float)GetTime() * 5f) + 1f) * 0.5f;
        var alpha = (byte)(18 + pulse * 42);
        
        return new Color((byte)255, (byte)165, (byte)0, alpha);
    }

    private static void Handle2DEditing(ref (int part, int vertex) hoveredVertex, ref (int part, int start, int end, Vector2 point) hoveredLine, ref int hoveredPart, ref Vector2? previewVertex) {

        if (Io.WantCaptureMouse) return;

        if (_selectedVertex != (-1, -1) || _selectedLine.HasValue || _selectedPart.HasValue) return;

        if (_rectStart.HasValue) {

            if (IsMouseButtonReleased(MouseButton.Left))
                FinishRectCreate();

            return;
        }

        var hasLinePoint = hoveredLine.part != -1;
        var canInsertVertex = IsKeyDown(KeyboardKey.LeftShift) && hasLinePoint;

        if (canInsertVertex) previewVertex = hoveredLine.point;

        if (IsMouseButtonDown(MouseButton.Right) && hoveredVertex != (-1, -1)) {

            if (_activePart == hoveredVertex.part && Map.Parts[hoveredVertex.part].Vertices.Count <= 3)
                _activePart = -1;

            Map.DeleteVertex(hoveredVertex);
            hoveredVertex = (-1, -1);
            return;
        }

        if (!IsMouseButtonPressed(MouseButton.Left)) return;

        if (hoveredVertex != (-1, -1)) {
            
            SelectOrInsertVertex(hoveredVertex);
            return;
        }

        if (hoveredPart != -1 && !IsKeyDown(KeyboardKey.LeftShift)) {
            
            TrySelectPart(hoveredPart);
            return;
        }

        if (canInsertVertex) {
            
            SelectOrInsertVertex((-1, -1));
            return;
        }

        if (hoveredLine.part != -1 && TrySelectLine(hoveredLine)) return;

        _rectStart = Snap(MouseWorldPos);
        _rectParentPart = hoveredPart;
    }

    private static bool TrySelectLine((int part, int start, int end, Vector2 point) hoveredLine) {

        var vertices = Map.Parts[hoveredLine.part].Vertices;
        var mouseStart = Snap(MouseWorldPos);

        _selectedLine = (
            
            (hoveredLine.part, hoveredLine.start, hoveredLine.end),
            mouseStart,
            vertices[hoveredLine.start],
            vertices[hoveredLine.end]
        );
        
        _activePart = hoveredLine.part;

        return true;
    }

    private static void TrySelectPart(int hoveredPart) {

        _activePart = hoveredPart;
        
        _selectedPart = (
            
            hoveredPart,
            Snap(MouseWorldPos),
            Map.Parts[hoveredPart].Vertices.Select(vertex => vertex).ToList()
        );
    }

    private static float GetVertexSelectDistance() => VertexSelectPixels / Render.Cam2D.Zoom;

    private static float GetLineSelectDistance() => LineSelectPixels / Render.Cam2D.Zoom;

    private static void FinishRectCreate() {

        if (!_rectStart.HasValue) return;

        var start = _rectStart.Value;
        var end = Snap(MouseWorldPos);

        _rectStart = null;
        var hoveredPart = _rectParentPart;
        _rectParentPart = -1;

        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);

        if (max.X - min.X <= float.Epsilon || max.Y - min.Y <= float.Epsilon) return;

        var part = new Part();

        part.Vertices.Add(new Vector2(min.X, min.Y));
        part.Vertices.Add(new Vector2(min.X, max.Y));
        part.Vertices.Add(new Vector2(max.X, max.Y));
        part.Vertices.Add(new Vector2(max.X, min.Y));

        if (hoveredPart != -1 && !part.Vertices.All(vertex => IsPointInPolygon(vertex, Map.Parts[hoveredPart].Vertices)))
            return;
        
        Map.Parts.Add(part);
        
        _activePart = Map.Parts.Count - 1;
    }

    private static void DrawRectPreview() {

        if (!_rectStart.HasValue) return;

        var start = _rectStart.Value;
        var end = Snap(MouseWorldPos);
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);

        var vertices = new List<Vector2> {
            
            new(min.X, min.Y),
            new(min.X, max.Y),
            new(max.X, max.Y),
            new(max.X, min.Y)
        };

        Render.Shape(vertices, Colors.Part, Colors.PartFail);

        for (var i = 0; i < vertices.Count; i++)
            DrawEdge(vertices, i);
    }

    private static void DrawEdge(List<Vector2> vertices, int index, bool hovered = false) {
        
        if (vertices.Count < 2) return;

        var next = index + 1;
        var color = hovered ? Colors.Orange : Colors.Gray;

        if (next < vertices.Count) {
            
            Render.Line(vertices[index], vertices[next], color);
            return;
        }

        if (vertices.Count > 2) Render.Line(vertices[index], vertices[0], color);
    }

    private static void DrawPartInspector() {

        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(new Vector2(340, GetScreenHeight()));

        if (!ImGui.Begin("Part", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)) {
            
            ImGui.End();
            return;
        }

        ImGui.Text(_mode3D ? "Mode: 3D" : "Mode: 2D");

        if (_activePart < 0 || _activePart >= Map.Parts.Count) {
            
            ImGui.Separator();
            ImGui.Text("No part selected.");
            ImGui.End();
            
            return;
        }

        var part = Map.Parts[_activePart];

        ImGui.Separator();
        ImGui.Text($"Selected Part: {_activePart}");

        var height = part.Height;
        
        if (ImGui.DragFloat("Height", ref height, DragSpeed, 0.1f, 1000f))
            part.Height = height;

        var yOffset = part.YOffset;
        
        if (ImGui.DragFloat("YOffset", ref yOffset, DragSpeed, -1000f, 1000f))
            part.YOffset = yOffset;

        DrawSurfaceEditor("Floor", part.Floor);
        DrawSurfaceEditor("Wall", part.Wall);
        DrawSurfaceEditor("Ceil", part.Ceil);

        ImGui.End();
    }

    private static void DrawSurfaceEditor(string label, Surface surface) {

        if (!ImGui.CollapsingHeader(label)) return;

        DrawTextureField($"{label} Texture", surface);

        var mode = (int)surface.Mode;
        var modeNames = Enum.GetNames<TileMode>();
        
        if (ImGui.Combo($"{label} Mode", ref mode, modeNames, modeNames.Length))
            surface.Mode = (TileMode)mode;

        var wrap = (int)surface.Wrap;
        var wrapNames = Enum.GetNames<TextureWrap>();
        
        if (ImGui.Combo($"{label} Wrap", ref wrap, wrapNames, wrapNames.Length))
            surface.Wrap = (TextureWrap)wrap;

        var tiling = surface.Tiling;
        
        if (ImGui.DragFloat2($"{label} Tiling", ref tiling, DragSpeed, 0.01f, 1000f))
            surface.Tiling = tiling;

        var offset = surface.Offset;
        
        if (ImGui.DragFloat2($"{label} Offset", ref offset, DragSpeed))
            surface.Offset = offset;
    }

    private static void DrawTextureField(string label, Surface surface) {

        var texture = surface.Texture;
        
        if (ImGui.InputText(label, ref texture, 256))
            surface.Texture = texture;

        if (TextureFiles.Length == 0)
            return;

        var currentIndex = Array.IndexOf(TextureFiles, surface.Texture);
        
        if (currentIndex < 0)
            currentIndex = 0;

        if (ImGui.Combo($"{label} Presets", ref currentIndex, TextureFiles, TextureFiles.Length))
            surface.Texture = TextureFiles[currentIndex];
    }
    
    public static bool IsPointInPolygon(Vector2 point, List<Vector2> vertices) {

        var inside = false;

        for (var i = 0; i < vertices.Count; i++) {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];

            var intersects = a.Y > point.Y != b.Y > point.Y
                             && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y + float.Epsilon) + a.X;

            if (intersects)
                inside = !inside;
        }

        return inside;
    }
}