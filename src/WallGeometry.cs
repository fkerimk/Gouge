using System.Numerics;

internal static class WallGeometry {

    public static List<WallFaceSegment> GetVisibleWallFaces(Map map, int currentPartIndex, Vector2 edgeStart, Vector2 edgeEnd, float currentBottom, float currentTop) {

        var splitPoints = new List<float> { 0f, 1f };
        var overlaps = new List<EdgeOverlap>();
        var delta = edgeEnd - edgeStart;
        var lengthSquared = delta.LengthSquared();

        if (lengthSquared <= float.Epsilon)
            return [];

        for (var partIndex = 0; partIndex < map.Parts.Count; partIndex++) {

                var part = map.Parts[partIndex];
                var vertices = part.Vertices;

            for (var vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++) {
                var nextIndex = Geometry2D.GetNextLoopIndex(vertexIndex, vertices.Count);

                if (partIndex == currentPartIndex && vertices[vertexIndex] == edgeStart && vertices[nextIndex] == edgeEnd)
                    continue;

                if (!TryGetOverlap(edgeStart, edgeEnd, vertices[vertexIndex], vertices[nextIndex], lengthSquared, out var overlap))
                    continue;

                splitPoints.Add(overlap.from);
                splitPoints.Add(overlap.to);
                overlaps.Add(new EdgeOverlap(
                    overlap.from,
                    overlap.to,
                    part.YOffset,
                    part.YOffset + part.Height
                ));
            }
        }

        splitPoints.Sort();

        var faces = new List<WallFaceSegment>();

        for (var i = 0; i < splitPoints.Count - 1; i++) {

            var from = splitPoints[i];
            var to = splitPoints[i + 1];

            if (to - from <= 0.0001f)
                continue;

            var sample = (from + to) * 0.5f;
            var occluders = overlaps
                .Where(overlap => overlap.From <= sample && sample <= overlap.To)
                .Select(overlap => new VerticalRange(
                    MathF.Max(currentBottom, overlap.Bottom),
                    MathF.Min(currentTop, overlap.Top)
                ))
                .Where(range => range.Top - range.Bottom > 0.0001f)
                .OrderBy(range => range.Bottom)
                .ToList();

            faces.AddRange(SubtractVerticalRanges(currentBottom, currentTop, occluders)
                .Select(verticalRange => new WallFaceSegment(from, to, verticalRange.Bottom, verticalRange.Top)));
        }

        return faces;
    }

    private static List<VerticalRange> SubtractVerticalRanges(float bottom, float top, List<VerticalRange> cuts) {

        var remaining = new List<VerticalRange> { new(bottom, top) };

        foreach (var cut in cuts) {

            for (var i = remaining.Count - 1; i >= 0; i--) {

                var range = remaining[i];
                var overlapBottom = MathF.Max(range.Bottom, cut.Bottom);
                var overlapTop = MathF.Min(range.Top, cut.Top);

                if (overlapTop - overlapBottom <= 0.0001f)
                    continue;

                remaining.RemoveAt(i);

                if (range.Bottom < overlapBottom - 0.0001f)
                    remaining.Insert(i, new VerticalRange(range.Bottom, overlapBottom));

                if (overlapTop < range.Top - 0.0001f)
                    remaining.Insert(i + (range.Bottom < overlapBottom - 0.0001f ? 1 : 0), new VerticalRange(overlapTop, range.Top));
            }
        }

        return remaining;
    }

    private static bool TryGetOverlap(Vector2 edgeStart, Vector2 edgeEnd, Vector2 otherStart, Vector2 otherEnd, float lengthSquared, out (float from, float to) overlap) {

        overlap = default;

        const float epsilon = 0.0001f;

        var delta = edgeEnd - edgeStart;
        var otherDelta = otherEnd - otherStart;

        if (MathF.Abs(Geometry2D.Cross(delta, otherDelta)) > epsilon)
            return false;

        if (MathF.Abs(Geometry2D.Cross(delta, otherStart - edgeStart)) > epsilon || MathF.Abs(Geometry2D.Cross(delta, otherEnd - edgeStart)) > epsilon)
            return false;

        var t0 = Vector2.Dot(otherStart - edgeStart, delta) / lengthSquared;
        var t1 = Vector2.Dot(otherEnd - edgeStart, delta) / lengthSquared;
        var from = MathF.Max(0f, MathF.Min(t0, t1));
        var to = MathF.Min(1f, MathF.Max(t0, t1));

        if (to - from <= epsilon)
            return false;

        overlap = (from, to);
        return true;
    }
    private readonly record struct EdgeOverlap(float From, float To, float Bottom, float Top);
    private readonly record struct VerticalRange(float Bottom, float Top);
}

public readonly record struct WallFaceSegment(float From, float To, float Bottom, float Top);
