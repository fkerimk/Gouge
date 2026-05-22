using System.Numerics;
using Raylib_cs;

internal static class Geometry2D {

    public static float Distance(Vector2 a, Vector2 b) => Raymath.Vector2Distance(a, b);

    private static float Dot(Vector2 a, Vector2 b) => Raymath.Vector2DotProduct(a, b);

    public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    public static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b, out Vector2 closestPoint) {

        var ab = b - a;
        var lengthSquared = Dot(ab, ab);

        if (lengthSquared <= float.Epsilon) {

            closestPoint = a;
            return Distance(point, a);
        }

        var t = Dot(point - a, ab) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);

        closestPoint = a + ab * t;
        return Distance(point, closestPoint);
    }

    public static int GetNextLoopIndex(int index, int count) =>
        index + 1 < count || count <= 2
            ? index + 1
            : 0;

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

    public static float SignedArea(List<Vector2> vertices) {

        var area = 0f;

        for (var i = 0; i < vertices.Count; i++) {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Count];
            area += current.X * next.Y - next.X * current.Y;
        }

        return area * 0.5f;
    }

    public static List<Vector2> CreateRectangleVertices(Vector2 min, Vector2 max) => [
        new(min.X, min.Y),
        new(min.X, max.Y),
        new(max.X, max.Y),
        new(max.X, min.Y)
    ];
}
