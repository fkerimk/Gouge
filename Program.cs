using System.Numerics;
using Sickle.Heart.Core;
using Sickle.Heart.Map;
using static Sickle.Heart.Core.Button;
using static Raylib_cs.Raylib;

internal static class Program {

    private static readonly Map Map = new();
    
    private static bool _mode3D;
    
    private const float Camera3DHeight = 2.5f;
    private const float Camera3DMoveSpeed = 6f;
    private const float Camera3DMouseSensitivity = 0.0035f;
    private const float Camera3DPitchLimit = 1.35f;
    private static float _camera3DYaw = -MathF.PI * 0.5f;
    private static float _camera3DPitch = -0.35f;
    
    private static (int part, int vertex) _selectedVertex = (-1, -1);
    private const float VertexSelectDistance = 0.15f;

    public static void Main() {
        
        Window.Open();

        CreateDefaultPart();

        while (Window.IsAlive()) {

            if (Input.IsButtonPressed(KeyBoardSpace))
                _mode3D = !_mode3D;
            
            if (!_mode3D) MoveSelectedVertex();
            
            Camera();
            Draw();
        }

        Window.Close();
    }

    private static void CreateDefaultPart() {
        
        var part = new Part();

        part.Vertices.Add(new Vector2(-5, -5));
        part.Vertices.Add(new Vector2(-5,  5));
        part.Vertices.Add(new Vector2( 5,  5));
        part.Vertices.Add(new Vector2( 5, -5));

        Map.Parts.Add(part);
    }

    private static void MoveSelectedVertex() {
        
        if (_selectedVertex == (-1, -1)) return;

        var snappedPos = Snap(Input.MouseWorldPos);

        Map.Parts[_selectedVertex.part].Vertices[_selectedVertex.vertex] = snappedPos;

        if (!Input.IsButtonUp(MouseLeft)) return;

        TryMergeVertex(snappedPos);
        
        _selectedVertex = (-1, -1);
    }

    private static Vector2 Snap(Vector2 position) {
        
        var snap = Input.IsButtonDown(KeyBoardLeftAlt) ? 10f : 1f;

        return new Vector2 (
            
            MathF.Round(position.X * snap) / snap,
            MathF.Round(position.Y * snap) / snap
        );
    }
    
    private static void TryMergeVertex(Vector2 position) {
        
        if (!Map.Parts
                .Select(part => part.Vertices)
                .Where((vertices, i) => vertices
                    .Where((_, j) => _selectedVertex != (i, j))
                    .Any(t => !(Vector2.DistanceSquared(t, position) > float.Epsilon * float.Epsilon)))
                .Any()) return;
        
        Map.DeleteVertex(_selectedVertex);
    }

    private static void Camera() {

        if (_mode3D) {

            if (Input.IsButtonDown(MouseRight)) {

                var mouseDelta = Input.MouseDelta;
                
                _camera3DYaw += mouseDelta.X * Camera3DMouseSensitivity;
                _camera3DPitch = Math.Clamp(_camera3DPitch - mouseDelta.Y * Camera3DMouseSensitivity, -Camera3DPitchLimit, Camera3DPitchLimit);
            }

            var forward = new Vector3(
                
                MathF.Cos(_camera3DPitch) * MathF.Cos(_camera3DYaw),
                MathF.Sin(_camera3DPitch),
                MathF.Cos(_camera3DPitch) * MathF.Sin(_camera3DYaw)
            );

            var forward2D = Vector2.Normalize(new Vector2(forward.X, forward.Z));
            var right2D = new Vector2(-forward2D.Y, forward2D.X);
            
            var move = Vector2.Zero;

            if (Input.IsButtonDown(KeyBoardW)) move += forward2D;
            if (Input.IsButtonDown(KeyBoardS)) move -= forward2D;
            if (Input.IsButtonDown(KeyBoardA)) move -= right2D;
            if (Input.IsButtonDown(KeyBoardD)) move += right2D;

            if (move != Vector2.Zero)
                Render.Cam2D.Target += Vector2.Normalize(move) * Camera3DMoveSpeed * GetFrameTime();

            Render.Cam3D.Position = new Vector3(Render.Cam2D.Target.X, Camera3DHeight, Render.Cam2D.Target.Y);
            Render.Cam3D.Target = Render.Cam3D.Position + forward;
            Render.Cam3D.Up = Vector3.UnitY;
            
        } else {
            
            if (Input.IsButtonDown(MouseMiddle))
                Render.Cam2D.Target -= Input.MouseDelta / Render.Cam2D.Zoom;

            if (Input.MouseScroll == 0) return;

            var mouseWorldBeforeZoom = Input.MouseWorldPos;

            Render.Cam2D.Zoom += Input.MouseScroll * Render.Cam2D.Zoom / 2f;
            Render.Cam2D.Zoom = MathF.Max(Render.Cam2D.Zoom, 2f);

            Render.Cam2D.Target = mouseWorldBeforeZoom - (Input.MousePos - Render.Cam2D.Offset) / Render.Cam2D.Zoom;

        }
    }

    private static void Draw() {
        
        Render.Start();

        if (_mode3D) {
            
            Render.Begin3D();
            
            Render.Map(Map);
            
            Render.End3D();
            
        } else {
            
            Render.Begin2D();

            DrawGrid();

            var hoveredVertex = _selectedVertex == (-1, -1)
                ? FindHoveredVertex()
                : (-1, -1);

            Vector2? previewVertex = null;

            if (_selectedVertex == (-1, -1)) {
            
                if (hoveredVertex == (-1, -1) && Map.TryFindPointOnLine(out var point, out _, out _))
                    previewVertex = point;

                if (Input.IsButtonDown(MouseRight) && hoveredVertex != (-1, -1)) {
                
                    Map.DeleteVertex(hoveredVertex);
                    hoveredVertex = (-1, -1);
                }
            
                else if (Input.IsButtonDown(MouseLeft))
                    SelectOrInsertVertex(hoveredVertex);
            }

            DrawParts(hoveredVertex);

            if (previewVertex.HasValue)
                Render.Square(Colors.Orange, previewVertex.Value, .1f);

            Render.End2D();
        }
        
        Render.Stop();
    }

    private static void DrawGrid() {
        
        if (Input.IsButtonDown(KeyBoardLeftAlt)) {
            
            Render.Grid(.1f, Colors.GridBottom);
            Render.Grid(1f, Colors.GridTop);
        }
        else Render.Grid(1f, Colors.GridBottom);
    }

    private static (int part, int vertex) FindHoveredVertex() {
        
        for (var i = 0; i < Map.Parts.Count; i++) {
            
            var vertices = Map.Parts[i].Vertices;

            for (var j = 0; j < vertices.Count; j++) {
                
                if (Util.Distance(Input.MouseWorldPos, vertices[j]) <= VertexSelectDistance)
                    return (i, j);
            }
        }

        return (-1, -1);
    }

    private static void SelectOrInsertVertex((int part, int vertex) hoveredVertex) {
        
        if (hoveredVertex != (-1, -1)) {
            
            _selectedVertex = hoveredVertex;
            return;
        }

        if (!Map.TryFindPointOnLine(out var point, out var partIndex, out var insertIndex)) return;

        Map.InsertVertex(partIndex, insertIndex, point);
        _selectedVertex = (partIndex, insertIndex);
    }

    private static void DrawParts((int part, int vertex) hoveredVertex) {
        
        for (var i = 0; i < Map.Parts.Count; i++) {
            
            var part = Map.Parts[i];
            var vertices = part.Vertices;

            Render.Shape(vertices, Colors.Part, Colors.PartFail);

            for (var j = 0; j < vertices.Count; j++) {
                
                var vertex = vertices[j];

                DrawEdge(vertices, j);

                var color = _selectedVertex == (i, j) || hoveredVertex == (i, j)
                    ? Colors.Orange
                    : Colors.White;

                Render.Square(color, vertex, .1f);
            }
        }
    }

    private static void DrawEdge(List<Vector2> vertices, int index) {
        
        if (vertices.Count < 2) return;

        var next = index + 1;

        if (next < vertices.Count) {
            
            Render.Line(vertices[index], vertices[next], Colors.Gray);
            return;
        }

        if (vertices.Count > 2) Render.Line(vertices[index], vertices[0], Colors.Gray);
    }
}
