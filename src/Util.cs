using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

public static class Util {

    public static Vector2 ScreenToWorld(Vector2 pos, Camera2D cam) => GetScreenToWorld2D(pos, cam);
    public static Ray ScreenToWorld(Vector2 pos, Camera3D cam) => GetScreenToWorldRay(pos, cam);
    
    public static float Distance(Vector2 a, Vector2 b) => Raymath.Vector2Distance(a, b);
    
    public static float Dot(Vector2 a, Vector2 b) => Raymath.Vector2DotProduct(a, b);
    
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
    
    public static bool TryFindPointOnLine(Vector2 targetPoint, Vector2 lineStart, Vector2 lineEnd, float selectDistance, out Vector2 point ) {
        
        point = Vector2.Zero;

        var distance = DistancePointToSegment(targetPoint, lineStart, lineEnd, out var closestPoint );

        if (distance > selectDistance) return false;

        point = closestPoint;
        
        return true;
    }

    public static bool TryGetNextVertexIndex(List<Vector2> vertices, int index, out int next) {
        
        next = index + 1;
        if (next < vertices.Count || vertices.Count <= 2) return true;
        next = 0;
        
        return true;
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
