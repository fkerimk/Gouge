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

    private static ImGuiIOPtr _io;
    private static Vector2 _mouseWorldPos;
    private static Vector2 _rightMousePressPos;
    private static bool _rightMouseDragged;
    private static (int part, int vertex, Vector2 screenStart)? _pendingVertex3D;
    private static ((int part, int start, int end, Vector2 point) line, Vector2 screenStart)? _pendingLine3D;
    private static (int part, Vector2 screenStart)? _pendingPart3D;

    private const float DragSpeed = .05f;
    
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
        
        while (!WindowShouldClose()) {

            if (IsKeyPressed(KeyboardKey.Space))
                _mode3D = !_mode3D;

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

                _hoveredPart3D = hoveredPart;

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

    private static void MoveSelectedVertex() {
        
        if (_selectedVertex == (-1, -1)) return;

        var snappedPos = Snap(_mouseWorldPos);

        Map.Parts[_selectedVertex.part].Vertices[_selectedVertex.vertex] = snappedPos;

        if (!IsMouseButtonUp(MouseButton.Left)) return;

        TryMergeVertex(snappedPos);
        
        _selectedVertex = (-1, -1);
    }

    private static void MoveSelectedLine() {

        if (!_selectedLine.HasValue) return;

        var selection = _selectedLine.Value;
        var delta = Snap(_mouseWorldPos) - selection.mouseStart;
        var vertices = Map.Parts[selection.edge.part].Vertices;

        vertices[selection.edge.start] = selection.startVertex + delta;
        vertices[selection.edge.end] = selection.endVertex + delta;

        if (!IsMouseButtonUp(MouseButton.Left)) return;

        _selectedLine = null;
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
        var delta = wheel * 0.25f;

        if (IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift))
            part.YOffset += delta;
        else part.Height = MathF.Max(0.25f, part.Height + delta);
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
