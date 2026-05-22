using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static partial class Gouge {

    private static (int part, int vertex) FindHoveredVertex() {

        var bestDistance = GetVertexSelectDistance();
        var hovered = (-1, -1);

        for (var i = 0; i < Map.Parts.Count; i++) {

            var vertices = Map.Parts[i].Vertices;

            for (var j = 0; j < vertices.Count; j++) {

                var distance = Geometry2D.Distance(_mouseWorldPos, vertices[j]);

                if (distance > bestDistance)
                    continue;

                bestDistance = distance;
                hovered = (i, j);
            }
        }

        return hovered;
    }

    private static (int part, int start, int end, Vector2 point) FindHoveredLine() =>
        !Map.TryFindLine(_mouseWorldPos, GetLineSelectDistance(), out var point, out var partIndex, out var startIndex, out var endIndex)
            ? (-1, -1, -1, Vector2.Zero)
            : (partIndex, startIndex, endIndex, point);

    private static void SelectOrInsertVertex((int part, int vertex) hoveredVertex) {

        if (hoveredVertex != (-1, -1)) {

            _selectedVertex = hoveredVertex;
            _activePart = hoveredVertex.part;
            return;
        }

        if (!Map.TryFindPointOnLine(_mouseWorldPos, GetLineSelectDistance(), out var point, out var partIndex, out var insertIndex))
            return;

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

                DrawEdge(vertices, j, hoveredLine.part == i && hoveredLine.start == j && hoveredLine.end == Geometry2D.GetNextLoopIndex(j, vertices.Count));

                var color = _selectedVertex == (i, j) || hoveredVertex == (i, j)
                    ? Colors.Orange
                    : Colors.White;

                Render.Square(color, vertex, .1f);
            }
        }
    }

    private static Color GetPartColor(int partIndex, int hoveredPart) {

        if (_activePart != partIndex)
            return hoveredPart == partIndex ? Colors.PartHover : Colors.Part;

        var pulse = (MathF.Sin((float)GetTime() * 5f) + 1f) * 0.5f;
        var alpha = (byte)(18 + pulse * 42);

        return new Color((byte)255, (byte)165, (byte)0, alpha);
    }

    private static void Handle2DEditing(ref (int part, int vertex) hoveredVertex, ref (int part, int start, int end, Vector2 point) hoveredLine, ref int hoveredPart, ref Vector2? previewVertex) {

        if (_io.WantCaptureMouse || _selectedVertex != (-1, -1) || _selectedLine.HasValue || _selectedPart.HasValue)
            return;

        if (_rectStart.HasValue) {

            if (IsMouseButtonReleased(MouseButton.Left))
                FinishRectCreate();

            return;
        }

        var hasLinePoint = hoveredLine.part != -1;
        var canInsertVertex = IsKeyDown(KeyboardKey.LeftShift) && hasLinePoint;

        if (canInsertVertex)
            previewVertex = hoveredLine.point;

        if (IsMouseButtonDown(MouseButton.Right) && hoveredVertex != (-1, -1)) {

            if (_activePart == hoveredVertex.part && Map.Parts[hoveredVertex.part].Vertices.Count <= 3)
                _activePart = -1;

            Map.DeleteVertex(hoveredVertex);
            hoveredVertex = (-1, -1);
            return;
        }

        if (!IsMouseButtonPressed(MouseButton.Left))
            return;

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

        if (hoveredLine.part != -1 && TrySelectLine(hoveredLine))
            return;

        _rectStart = Snap(_mouseWorldPos);
        _rectParentPart = hoveredPart;
    }

    private static bool TrySelectLine((int part, int start, int end, Vector2 point) hoveredLine) {

        var vertices = Map.Parts[hoveredLine.part].Vertices;
        var mouseStart = Snap(_mouseWorldPos);

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
            Snap(_mouseWorldPos),
            Map.Parts[hoveredPart].Vertices.ToList()
        );
    }

    private static float GetVertexSelectDistance() => VertexSelectPixels / Render.Cam2D.Zoom;

    private static float GetLineSelectDistance() => LineSelectPixels / Render.Cam2D.Zoom;

    private static void FinishRectCreate() {

        if (!_rectStart.HasValue)
            return;

        var start = _rectStart.Value;
        var end = Snap(_mouseWorldPos);

        _rectStart = null;
        var hoveredPart = _rectParentPart;
        _rectParentPart = -1;

        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);

        if (max.X - min.X <= float.Epsilon || max.Y - min.Y <= float.Epsilon)
            return;

        var part = CreateRectanglePart(min, max);

        if (hoveredPart != -1 && !part.Vertices.All(vertex => Geometry2D.IsPointInPolygon(vertex, Map.Parts[hoveredPart].Vertices)))
            return;

        Map.Parts.Add(part);
        _activePart = Map.Parts.Count - 1;
    }

    private static void DrawRectPreview() {

        if (!_rectStart.HasValue)
            return;

        var start = _rectStart.Value;
        var end = Snap(_mouseWorldPos);
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);
        var vertices = Geometry2D.CreateRectangleVertices(min, max);

        Render.Shape(vertices, Colors.Part, Colors.PartFail);

        for (var i = 0; i < vertices.Count; i++)
            DrawEdge(vertices, i);
    }

    private static void DrawEdge(List<Vector2> vertices, int index, bool hovered = false) {

        if (vertices.Count < 2)
            return;

        var next = Geometry2D.GetNextLoopIndex(index, vertices.Count);
        var color = hovered ? Colors.Orange : Colors.Gray;

        if (next >= vertices.Count)
            return;

        Render.Line(vertices[index], vertices[next], color);
    }
}
