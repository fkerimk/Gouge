using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

public static class Picking {

    public static bool TryRaycastPart(Map map, Ray ray, out int partIndex, out float distance) {

        partIndex = -1;
        distance = float.PositiveInfinity;

        for (var i = 0; i < map.Parts.Count; i++) {

            if (!TryRaycastPart(map, i, ray, out var hitDistance) || hitDistance >= distance)
                continue;

            distance = hitDistance;
            partIndex = i;
        }

        return partIndex != -1;
    }

    private static bool TryRaycastPart(Map map, int partIndex, Ray ray, out float distance) {

        distance = float.PositiveInfinity;
        var part = map.Parts[partIndex];

        if (part.Vertices.Count < 3)
            return false;

        var floorY = part.YOffset;
        var ceilY = part.YOffset + part.Height;
        var hit = false;

        if (TryHitHorizontalSurface(ray, part.Vertices, floorY, out var floorDistance))
            hit |= TryUpdateDistance(floorDistance, ref distance);

        if (TryHitHorizontalSurface(ray, part.Vertices, ceilY, out var ceilDistance))
            hit |= TryUpdateDistance(ceilDistance, ref distance);

        if (TryHitWalls(map, partIndex, ray, out var wallDistance))
            hit |= TryUpdateDistance(wallDistance, ref distance);

        return hit;
    }

    private static bool TryHitHorizontalSurface(Ray ray, List<Vector2> vertices, float y, out float bestDistance) {

        bestDistance = float.PositiveInfinity;

        if (MathF.Abs(ray.Direction.Y) <= float.Epsilon)
            return false;

        var distance = (y - ray.Position.Y) / ray.Direction.Y;

        if (distance < 0f)
            return false;

        var hitPoint = ray.Position + ray.Direction * distance;
        var point2D = new Vector2(hitPoint.X, hitPoint.Z);

        if (!Util.IsPointInPolygon(point2D, vertices))
            return false;

        bestDistance = distance;
        return true;
    }

    private static bool TryHitWalls(Map map, int partIndex, Ray ray, out float bestDistance) {

        bestDistance = float.PositiveInfinity;
        var hit = false;
        var part = map.Parts[partIndex];
        var bottom = part.YOffset;
        var top = part.YOffset + part.Height;

        for (var i = 0; i < part.Vertices.Count; i++) {

            var edgeStart = part.Vertices[i];
            var edgeEnd = part.Vertices[(i + 1) % part.Vertices.Count];

            foreach (var face in WallGeometry.GetVisibleWallFaces(map, partIndex, edgeStart, edgeEnd, bottom, top)) {
                var start = Vector2.Lerp(edgeStart, edgeEnd, face.From);
                var end = Vector2.Lerp(edgeStart, edgeEnd, face.To);

                var bottomStart = new Vector3(start.X, face.Bottom, start.Y);
                var bottomEnd = new Vector3(end.X, face.Bottom, end.Y);
                var topStart = new Vector3(start.X, face.Top, start.Y);
                var topEnd = new Vector3(end.X, face.Top, end.Y);

                hit |= TryUpdateDistance(GetRayCollisionTriangle(ray, bottomStart, topStart, topEnd), ref bestDistance);
                hit |= TryUpdateDistance(GetRayCollisionTriangle(ray, bottomStart, topEnd, bottomEnd), ref bestDistance);
            }
        }

        return hit;
    }

    private static bool TryUpdateDistance(RayCollision collision, ref float bestDistance) {

        if (!collision.Hit || collision.Distance >= bestDistance)
            return false;

        bestDistance = collision.Distance;
        return true;
    }

    private static bool TryUpdateDistance(float distance, ref float bestDistance) {

        if (distance >= bestDistance)
            return false;

        bestDistance = distance;
        return true;
    }
}
