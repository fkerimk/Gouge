using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static partial class Gouge {

    private const float HandleSize3D = 0.15f;
    private const int GridRadius3D = 24;
    private const float OverlayYOffset3D = 0.04f;
    private const float OverlayHandleSize3D = 0.18f;
    private const float VertexSelectPixels3D = 38f;
    private const float LineSelectPixels3D = 32f;
    private const float DragStartPixels3D = 2f;
    private const float VertexPriorityPixels3D = 24f;

    private static Ray GetMouseRay3D() => RaylibUtil.ScreenToWorld(GetMousePosition(), Render.Cam3D);
    private static Ray GetMouseRay3D(Vector2 screenPos) => RaylibUtil.ScreenToWorld(screenPos, Render.Cam3D);

    private readonly record struct VertexHover3D((int part, int vertex) Hovered, float Score);
    private readonly record struct LineHover3D((int part, int start, int end, Vector2 point) Hovered, float Score);

    private static (int part, float distance) FindHoveredPartHit3D() {

        if (_io.WantCaptureMouse)
            return (-1, float.PositiveInfinity);

        var ray = GetMouseRay3D();
        return Picking.TryRaycastPart(Map, ray, out var partIndex, out _, out var distance)
            ? (partIndex, distance)
            : (-1, float.PositiveInfinity);
    }

    private static int FindHoveredPart3D() => FindHoveredPartHit3D().part;

    private static (int part, int vertex) FindHoveredVertex3D(int preferredPart = -1, float maxRayDistance = float.PositiveInfinity) =>
        FindHoveredVertexCandidate3D(preferredPart, maxRayDistance)?.Hovered ?? (-1, -1);

    private static VertexHover3D? FindHoveredVertexCandidate3D(int preferredPart = -1, float maxRayDistance = float.PositiveInfinity) {

        var bestDistance = VertexSelectPixels3D;
        var bestScore = float.PositiveInfinity;
        var hovered = (-1, -1);
        var mouse = GetMousePosition();
        var ray = GetMouseRay3D();
        var partOrder = GetPickPartOrder3D(preferredPart).ToArray();

        foreach (var i in partOrder) {
            var part = Map.Parts[i];

            for (var j = 0; j < part.Vertices.Count; j++) {
                var vertex = part.Vertices[j];
                var vertex3D = new Vector3(vertex.X, part.YOffset, vertex.Y);

                if (!IsDepthSelectable3D(ray, vertex3D, maxRayDistance))
                    continue;

                var screen = GetWorldToScreen(vertex3D, Render.Cam3D);
                var distance = Vector2.Distance(mouse, screen);
                var score = distance / VertexSelectPixels3D;

                if (distance > bestDistance || score >= bestScore)
                    continue;

                bestDistance = distance;
                bestScore = score;
                hovered = (i, j);
            }
        }

        return hovered == (-1, -1) ? null : new VertexHover3D(hovered, bestScore);
    }

    private static (int part, int start, int end, Vector2 point) FindHoveredLine3D(int preferredPart = -1, float maxRayDistance = float.PositiveInfinity) =>
        FindHoveredLineCandidate3D(preferredPart, maxRayDistance)?.Hovered ?? (part: -1, start: -1, end: -1, point: Vector2.Zero);

    private static LineHover3D? FindHoveredLineCandidate3D(int preferredPart = -1, float maxRayDistance = float.PositiveInfinity) {

        var bestScore = float.PositiveInfinity;
        var bestRayDistance = float.PositiveInfinity;
        var hovered = (part: -1, start: -1, end: -1, point: Vector2.Zero);
        var ray = GetMouseRay3D();

        foreach (var i in GetPickPartOrder3D(preferredPart)) {
            var part = Map.Parts[i];
            var vertices = part.Vertices;

            if (vertices.Count < 2)
                continue;

            for (var j = 0; j < vertices.Count; j++) {
                var next = Geometry2D.GetNextLoopIndex(j, vertices.Count);
                var start3D = new Vector3(vertices[j].X, part.YOffset, vertices[j].Y);
                var end3D = new Vector3(vertices[next].X, part.YOffset, vertices[next].Y);

                if (!TryGetRayToSegmentDistance(ray, start3D, end3D, out var segmentPoint, out var rayDistance, out var segmentDistance))
                    continue;

                var selectRadius = GetLineSelectRadius3D(segmentPoint);

                if (segmentDistance > selectRadius)
                    continue;

                if (!IsRayDistanceSelectable3D(rayDistance, maxRayDistance))
                    continue;

                var score = segmentDistance / selectRadius;

                if (score > bestScore || score >= bestScore && rayDistance >= bestRayDistance)
                    continue;

                bestScore = score;
                bestRayDistance = rayDistance;
                hovered = (i, j, next, new Vector2(segmentPoint.X, segmentPoint.Z));
            }
        }

        return hovered.part == -1 ? null : new LineHover3D(hovered, bestScore);
    }

    private static void ResolveHoveredGeometry3D(
        int preferredPart,
        float maxRayDistance,
        out (int part, int vertex) hoveredVertex,
        out (int part, int start, int end, Vector2 point) hoveredLine
    ) {

        var vertexCandidate = FindHoveredVertexCandidate3D(preferredPart, maxRayDistance);
        var lineCandidate = FindHoveredLineCandidate3D(preferredPart, maxRayDistance);

        if (vertexCandidate.HasValue && (
            !lineCandidate.HasValue
            || GetVertexScreenDistance3D(vertexCandidate.Value.Hovered) <= VertexPriorityPixels3D
            || vertexCandidate.Value.Score <= lineCandidate.Value.Score + 0.45f
        )) {
            hoveredVertex = vertexCandidate.Value.Hovered;
            hoveredLine = (part: -1, start: -1, end: -1, point: Vector2.Zero);
            return;
        }

        hoveredVertex = (-1, -1);
        hoveredLine = lineCandidate?.Hovered ?? (part: -1, start: -1, end: -1, point: Vector2.Zero);
    }

    private static bool TryUpdateMouseWorldPos3D() {

        var ray = GetMouseRay3D();

        if (_selectedVertex != (-1, -1))
            return TryGetMouseWorldPosOnPlane3D(Map.Parts[_selectedVertex.part].YOffset, ray, out _mouseWorldPos);

        if (_selectedLine.HasValue)
            return TryGetMouseWorldPosOnPlane3D(Map.Parts[_selectedLine.Value.edge.part].YOffset, ray, out _mouseWorldPos);

        if (_selectedPart.HasValue)
            return TryGetMouseWorldPosOnPlane3D(Map.Parts[_selectedPart.Value.part].YOffset, ray, out _mouseWorldPos);

        if (_rectStart.HasValue)
            return TryGetMouseWorldPosOnPlane3D(GetEditPlaneY(), ray, out _mouseWorldPos);

        if (TryGetHoverPlaneContext3D(out var planeY, out var planePoint)) {
            _mouseWorldPos = planePoint;
            return true;
        }

        return TryGetMouseWorldPosOnPlane3D(GetEditPlaneY(), ray, out _mouseWorldPos);
    }

    private static bool TryGetMouseWorldPosOnPlane3D(float y, Ray ray, out Vector2 point) {

        point = Vector2.Zero;

        if (!Picking.TryIntersectHorizontalPlane(ray, y, out var hitPoint))
            return false;

        point = new Vector2(hitPoint.X, hitPoint.Z);
        return true;
    }

    private static bool TryGetHoverPlaneContext3D(out float planeY, out Vector2 point) {

        var ray = GetMouseRay3D();

        if (Picking.TryRaycastPart(Map, ray, out var hitPart, out _, out _)) {
            planeY = Map.Parts[hitPart].YOffset;
            return TryGetMouseWorldPosOnPlane3D(planeY, ray, out point);
        }

        planeY = GetEditPlaneY();
        return TryGetMouseWorldPosOnPlane3D(planeY, ray, out point);
    }

    private static float GetHoverPlaneY3D() =>
        !TryGetHoverPlaneContext3D(out var planeY, out _)
            ? GetEditPlaneY()
            : planeY;

    private static Vector2 GetSelectionStartPoint(int partIndex) {

        if (!_mode3D)
            return Snap(_mouseWorldPos);

        var ray = GetMouseRay3D();
        return TryGetMouseWorldPosOnPlane3D(Map.Parts[partIndex].YOffset, ray, out var point)
            ? Snap(point)
            : Snap(_mouseWorldPos);
    }

    private static Vector2 GetSelectionStartPoint(int partIndex, Vector2 screenPos) {

        if (!_mode3D)
            return Snap(_mouseWorldPos);

        var ray = GetMouseRay3D(screenPos);
        return TryGetMouseWorldPosOnPlane3D(Map.Parts[partIndex].YOffset, ray, out var point)
            ? Snap(point)
            : Snap(_mouseWorldPos);
    }

    private static float GetEditPlaneY() {

        if (_selectedVertex != (-1, -1))
            return Map.Parts[_selectedVertex.part].YOffset;

        if (_selectedLine.HasValue)
            return Map.Parts[_selectedLine.Value.edge.part].YOffset;

        if (_selectedPart.HasValue)
            return Map.Parts[_selectedPart.Value.part].YOffset;

        if (_rectParentPart != -1)
            return Map.Parts[_rectParentPart].YOffset;

        if (_activePart != -1 && _activePart < Map.Parts.Count)
            return Map.Parts[_activePart].YOffset;

        return 0f;
    }

    private static bool IsOnPlane3D(Part part, float planeY) => MathF.Abs(part.YOffset - planeY) <= 0.001f;

    private static int FindBestPartOnPlane3D(Vector2 point, float planeY) {

        var bestIndex = -1;
        var bestArea = float.PositiveInfinity;

        for (var i = 0; i < Map.Parts.Count; i++) {
            var part = Map.Parts[i];

            if (!IsOnPlane3D(part, planeY) || !Geometry2D.IsPointInPolygonOrOnEdge(point, part.Vertices))
                continue;

            var area = MathF.Abs(Geometry2D.SignedArea(part.Vertices));

            if (area >= bestArea)
                continue;

            bestArea = area;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static int FindPreferredPartOnHoverPlane3D(int fallbackPart = -1) {

        if (TryGetHoverPlaneContext3D(out var planeY, out var point)) {
            var containedPart = FindBestPartOnPlane3D(point, planeY);

            if (containedPart != -1)
                return containedPart;
        }

        return fallbackPart;
    }

    private static IEnumerable<int> GetPlanePartOrder3D(float planeY) {

        var containing = new List<(int part, float area)>();
        var others = new List<(int part, float area)>();

        var hasPlanePoint = TryGetHoverPlaneContext3D(out _, out var point);

        for (var i = 0; i < Map.Parts.Count; i++) {
            var part = Map.Parts[i];

            if (!IsOnPlane3D(part, planeY))
                continue;

            var entry = (i, MathF.Abs(Geometry2D.SignedArea(part.Vertices)));

            if (hasPlanePoint && Geometry2D.IsPointInPolygonOrOnEdge(point, part.Vertices))
                containing.Add(entry);
            else others.Add(entry);
        }

        foreach (var (part, _) in containing.OrderBy(entry => entry.area))
            yield return part;

        foreach (var (part, _) in others.OrderBy(entry => entry.area))
            yield return part;
    }

    private static IEnumerable<int> GetLinePartOrder3D(int preferredPart) {

        var yielded = new HashSet<int>();

        if (preferredPart != -1) {
            yield return preferredPart;
            yield break;
        }

        if (_activePart != -1 && _activePart < Map.Parts.Count && yielded.Add(_activePart))
            yield return _activePart;

        for (var i = 0; i < Map.Parts.Count; i++) {
            if (yielded.Add(i))
                yield return i;
        }
    }

    private static IEnumerable<int> GetPickPartOrder3D(int preferredPart) {

        var yielded = new HashSet<int>();

        if (TryGetHoverPlaneContext3D(out var planeY, out var hoverPoint)) {
            foreach (var partIndex in GetContainedPartOrder3D(hoverPoint, planeY)) {
                if (yielded.Add(partIndex))
                    yield return partIndex;
            }
        }

        if (preferredPart != -1 && yielded.Add(preferredPart))
            yield return preferredPart;

        foreach (var partIndex in GetLinePartOrder3D(preferredPart)) {
            if (yielded.Add(partIndex))
                yield return partIndex;
        }
    }

    private static IEnumerable<int> GetContainedPartOrder3D(Vector2 point, float planeY) {

        var containing = new List<(int part, float area)>();

        for (var i = 0; i < Map.Parts.Count; i++) {
            var part = Map.Parts[i];

            if (!IsOnPlane3D(part, planeY) || !Geometry2D.IsPointInPolygonOrOnEdge(point, part.Vertices))
                continue;

            containing.Add((i, MathF.Abs(Geometry2D.SignedArea(part.Vertices))));
        }

        foreach (var (part, _) in containing.OrderBy(entry => entry.area))
            yield return part;
    }

    private static bool TryGetRayToSegmentDistance(Ray ray, Vector3 start, Vector3 end, out Vector3 segmentPoint, out float rayDistance, out float segmentDistance) {

        segmentPoint = Vector3.Zero;
        rayDistance = 0f;
        segmentDistance = float.PositiveInfinity;

        var u = Vector3.Normalize(ray.Direction);
        var v = end - start;
        var w = ray.Position - start;

        var a = Vector3.Dot(u, u);
        var b = Vector3.Dot(u, v);
        var c = Vector3.Dot(v, v);
        var d = Vector3.Dot(u, w);
        var e = Vector3.Dot(v, w);
        var denominator = a * c - b * b;

        if (c <= float.Epsilon)
            return false;

        float s;
        float t;

        if (MathF.Abs(denominator) <= 0.0001f) {
            s = 0f;
            t = Math.Clamp(e / c, 0f, 1f);
        } else {
            s = (b * e - c * d) / denominator;
            t = Math.Clamp((a * e - b * d) / denominator, 0f, 1f);

            if (s < 0f) {
                s = 0f;
                t = Math.Clamp(e / c, 0f, 1f);
            }
        }

        var closestPointOnRay = ray.Position + u * s;
        segmentPoint = start + v * t;
        rayDistance = s;
        segmentDistance = Vector3.Distance(closestPointOnRay, segmentPoint);
        return true;
    }

    private static float GetLineSelectRadius3D(Vector3 point) {

        var distanceToCamera = Vector3.Distance(Render.Cam3D.Position, point);
        var fovRadians = Render.Cam3D.FovY * MathF.PI / 180f;
        var worldPerPixel = 2f * distanceToCamera * MathF.Tan(fovRadians * 0.5f) / GetScreenHeight();

        return worldPerPixel * LineSelectPixels3D;
    }

    private static float GetVertexScreenDistance3D((int part, int vertex) hoveredVertex) {

        var part = Map.Parts[hoveredVertex.part];
        var vertex = part.Vertices[hoveredVertex.vertex];
        var screen = GetWorldToScreen(new Vector3(vertex.X, part.YOffset, vertex.Y), Render.Cam3D);
        return Vector2.Distance(GetMousePosition(), screen);
    }

    private static bool IsDepthSelectable3D(Ray ray, Vector3 point, float maxRayDistance) {

        if (!float.IsFinite(maxRayDistance))
            return true;

        var rayDepth = Vector3.Dot(point - ray.Position, Vector3.Normalize(ray.Direction));
        return IsRayDistanceSelectable3D(rayDepth, maxRayDistance);
    }

    private static bool IsRayDistanceSelectable3D(float candidateDistance, float maxRayDistance) =>
        !float.IsFinite(maxRayDistance)
        || candidateDistance <= maxRayDistance + GetDepthSelectSlack3D(maxRayDistance);

    private static float GetDepthSelectSlack3D(float hitDistance) => MathF.Max(0.05f, hitDistance * 0.01f);

    private static bool HasPending3DDrag() =>
        _pendingVertex3D.HasValue || _pendingLine3D.HasValue || _pendingPart3D.HasValue;

    private static void QueueVertexDrag3D((int part, int vertex) hoveredVertex) {

        _activePart = hoveredVertex.part;
        _pendingVertex3D = (hoveredVertex.part, hoveredVertex.vertex, GetMousePosition());
        _pendingLine3D = null;
        _pendingPart3D = null;
    }

    private static void QueueLineDrag3D((int part, int start, int end, Vector2 point) hoveredLine) {

        _activePart = hoveredLine.part;
        _pendingLine3D = (hoveredLine, GetMousePosition());
        _pendingVertex3D = null;
        _pendingPart3D = null;
    }

    private static void QueuePartDrag3D(int hoveredPart) {

        _activePart = hoveredPart;
        _pendingPart3D = (hoveredPart, GetMousePosition());
        _pendingVertex3D = null;
        _pendingLine3D = null;
    }

    private static void ProcessPending3DDrag() {

        if (!HasPending3DDrag())
            return;

        if (!IsMouseButtonDown(MouseButton.Left)) {
            _pendingVertex3D = null;
            _pendingLine3D = null;
            _pendingPart3D = null;
            return;
        }

        if (Vector2.Distance(GetMousePosition(), GetPendingDragStartScreen3D()) < DragStartPixels3D)
            return;

        if (_pendingVertex3D.HasValue) {
            var pending = _pendingVertex3D.Value;
            _selectedVertex = (pending.part, pending.vertex);
        }
        else if (_pendingLine3D.HasValue) {
            var pending = _pendingLine3D.Value;
            var vertices = Map.Parts[pending.line.part].Vertices;
            _selectedLine = (
                (pending.line.part, pending.line.start, pending.line.end),
                GetSelectionStartPoint(pending.line.part, pending.screenStart),
                vertices[pending.line.start],
                vertices[pending.line.end]
            );
        }
        else if (_pendingPart3D.HasValue) {
            var pending = _pendingPart3D.Value;
            _selectedPart = (
                pending.part,
                GetSelectionStartPoint(pending.part, pending.screenStart),
                Map.Parts[pending.part].Vertices.ToList()
            );
        }

        _pendingVertex3D = null;
        _pendingLine3D = null;
        _pendingPart3D = null;
    }

    private static Vector2 GetPendingDragStartScreen3D() =>
        _pendingVertex3D?.screenStart
        ?? _pendingLine3D?.screenStart
        ?? _pendingPart3D?.screenStart
        ?? GetMousePosition();

    private static void DrawGrid3D() {

        var spacing = IsKeyDown(KeyboardKey.LeftAlt) ? 0.1f : 1f;
        var y = GetEditPlaneY();

        DrawGrid3D(spacing, y, Colors.GridBottom);

        if (IsKeyDown(KeyboardKey.LeftAlt))
            DrawGrid3D(1f, y, Colors.GridTop);
    }

    private static void DrawGrid3D(float spacing, float y, Color color) {

        var target = Render.Cam2D.Target;
        var minX = MathF.Floor((target.X - GridRadius3D) / spacing) * spacing;
        var maxX = MathF.Ceiling((target.X + GridRadius3D) / spacing) * spacing;
        var minZ = MathF.Floor((target.Y - GridRadius3D) / spacing) * spacing;
        var maxZ = MathF.Ceiling((target.Y + GridRadius3D) / spacing) * spacing;

        for (var x = minX; x <= maxX; x += spacing)
            DrawLine3D(new Vector3(x, y, minZ), new Vector3(x, y, maxZ), color);

        for (var z = minZ; z <= maxZ; z += spacing)
            DrawLine3D(new Vector3(minX, y, z), new Vector3(maxX, y, z), color);
    }

    private static void DrawParts3D((int part, int vertex) hoveredVertex, (int part, int start, int end, Vector2 point) hoveredLine, int hoveredPart) {

        var planeY = GetHoverPlaneY3D();
        DrawPlanOverlay3D(planeY, hoveredVertex, hoveredLine, hoveredPart);

        for (var i = 0; i < Map.Parts.Count; i++) {
            var part = Map.Parts[i];

            if (i == hoveredPart && i != _activePart)
                Render.PartOutline(part, Colors.Gold);

            if (i == _activePart)
                Render.PartOutline(part, Colors.Orange);
        }
    }

    private static void DrawPlanOverlay3D(float planeY, (int part, int vertex) hoveredVertex, (int part, int start, int end, Vector2 point) hoveredLine, int hoveredPart) {

        foreach (var i in GetPlanePartOrder3D(planeY).Reverse()) {
            var part = Map.Parts[i];
            var vertices = part.Vertices;
            var y = hoveredLine.part == i ? part.YOffset : part.YOffset + OverlayYOffset3D;

            var defaultEdgeColor = i == _activePart
                ? Colors.Orange
                : i == hoveredPart
                    ? Colors.Gold
                    : Colors.White;

            for (var j = 0; j < vertices.Count; j++) {
                var vertex = vertices[j];
                var next = Geometry2D.GetNextLoopIndex(j, vertices.Count);
                var edgeColor = hoveredLine.part == i && hoveredLine.start == j && hoveredLine.end == next
                    ? Colors.Orange
                    : defaultEdgeColor;

                DrawLine3D(
                    new Vector3(vertex.X, y, vertex.Y),
                    new Vector3(vertices[next].X, y, vertices[next].Y),
                    edgeColor
                );

                if (hoveredLine.part == i && hoveredLine.start == j && hoveredLine.end == next)
                    DrawHoveredLineMarker3D(vertex, vertices[next], part.YOffset);

                var isHoveredVertex = hoveredVertex == (i, j);
                var isSelectedVertex = _selectedVertex == (i, j);
                var isSelectedPartVertex = i == _activePart;
                var color = isSelectedVertex
                    ? Colors.Ivory
                    : isHoveredVertex
                        ? Colors.Orange
                        : isSelectedPartVertex
                            ? Colors.Gold
                            : defaultEdgeColor;

                var size = isSelectedVertex || isHoveredVertex
                    ? OverlayHandleSize3D
                    : HandleSize3D;

                DrawCubeV(new Vector3(vertex.X, y, vertex.Y), new Vector3(size), color);
            }
        }
    }

    private static void DrawRectPreview3D() {

        if (!_rectStart.HasValue)
            return;

        var start = _rectStart.Value;
        var end = Snap(_mouseWorldPos);
        var min = Vector2.Min(start, end);
        var max = Vector2.Max(start, end);
        var vertices = Geometry2D.CreateRectangleVertices(min, max);
        var y = (_rectParentPart != -1 ? Map.Parts[_rectParentPart].YOffset : GetEditPlaneY()) + OverlayYOffset3D;

        for (var i = 0; i < vertices.Count; i++) {
            var next = Geometry2D.GetNextLoopIndex(i, vertices.Count);
            DrawLine3D(
                new Vector3(vertices[i].X, y, vertices[i].Y),
                new Vector3(vertices[next].X, y, vertices[next].Y),
                Colors.Orange
            );

            DrawCubeV(new Vector3(vertices[i].X, y, vertices[i].Y), new Vector3(OverlayHandleSize3D), Colors.Orange);
        }
    }

    private static void DrawHoveredLineMarker3D(Vector2 start, Vector2 end, float y) {

        DrawCylinderEx(
            new Vector3(start.X, y, start.Y),
            new Vector3(end.X, y, end.Y),
            0.035f,
            0.035f,
            8,
            Colors.Orange
        );
    }
}
