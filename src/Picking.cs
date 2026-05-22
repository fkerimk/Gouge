using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

internal static class Picking {

    private const float DistanceTieEpsilon = 0.0001f;

    public static bool TryRaycastPart(Map map, Ray ray, out int partIndex, out float distance) {

        var hit = TryRaycastPart(map, ray, out partIndex, out _, out distance);
        return hit;
    }

    public static bool TryRaycastPart(Map map, Ray ray, out int partIndex, out Vector3 hitPoint, out float distance) {

        partIndex = -1;
        hitPoint = Vector3.Zero;
        distance = float.PositiveInfinity;

        for (var i = 0; i < map.Parts.Count; i++) {

            if (!TryRaycastPart(map, i, ray, out var partHitPoint, out var hitDistance))
                continue;

            if (hitDistance > distance + DistanceTieEpsilon)
                continue;

            if (MathF.Abs(hitDistance - distance) <= DistanceTieEpsilon && partIndex != -1) {
                var currentArea = MathF.Abs(Geometry2D.SignedArea(map.Parts[partIndex].Vertices));
                var candidateArea = MathF.Abs(Geometry2D.SignedArea(map.Parts[i].Vertices));

                if (candidateArea >= currentArea)
                    continue;
            }

            distance = hitDistance;
            partIndex = i;
            hitPoint = partHitPoint;
        }

        return partIndex != -1;
    }

    public static bool TryIntersectHorizontalPlane(Ray ray, float y, out Vector3 hitPoint) {

        hitPoint = Vector3.Zero;

        if (MathF.Abs(ray.Direction.Y) <= float.Epsilon)
            return false;

        var distance = (y - ray.Position.Y) / ray.Direction.Y;

        if (distance < 0f)
            return false;

        hitPoint = ray.Position + ray.Direction * distance;
        return true;
    }

    private static bool TryRaycastPart(Map map, int partIndex, Ray ray, out Vector3 hitPoint, out float distance) {

        hitPoint = Vector3.Zero;
        distance = float.PositiveInfinity;
        var part = map.Parts[partIndex];

        if (part.Vertices.Count < 3)
            return false;

        var floorY = part.YOffset;
        var ceilY = part.YOffset + part.Height;
        var hit = false;

        if (TryHitHorizontalSurface(ray, part.Vertices, floorY, out var floorHitPoint, out var floorDistance))
            hit |= TryUpdateHit(floorHitPoint, floorDistance, ref hitPoint, ref distance);

        if (TryHitHorizontalSurface(ray, part.Vertices, ceilY, out var ceilHitPoint, out var ceilDistance))
            hit |= TryUpdateHit(ceilHitPoint, ceilDistance, ref hitPoint, ref distance);

        if (TryHitWalls(map, partIndex, ray, out var wallHitPoint, out var wallDistance))
            hit |= TryUpdateHit(wallHitPoint, wallDistance, ref hitPoint, ref distance);

        return hit;
    }

    private static bool TryHitHorizontalSurface(Ray ray, List<Vector2> vertices, float y, out Vector3 hitPoint, out float bestDistance) {

        hitPoint = Vector3.Zero;
        bestDistance = float.PositiveInfinity;

        if (!TryIntersectHorizontalPlane(ray, y, out hitPoint))
            return false;
        
        var point2D = new Vector2(hitPoint.X, hitPoint.Z);

        if (!Geometry2D.IsPointInPolygon(point2D, vertices))
            return false;

        var distance = Vector3.Distance(ray.Position, hitPoint);
        bestDistance = distance;
        return true;
    }

    private static bool TryHitWalls(Map map, int partIndex, Ray ray, out Vector3 hitPoint, out float bestDistance) {

        hitPoint = Vector3.Zero;
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

                hit |= TryUpdateHit(GetRayCollisionTriangle(ray, bottomStart, topStart, topEnd), ref hitPoint, ref bestDistance);
                hit |= TryUpdateHit(GetRayCollisionTriangle(ray, bottomStart, topEnd, bottomEnd), ref hitPoint, ref bestDistance);
            }
        }

        return hit;
    }

    private static bool TryUpdateHit(RayCollision collision, ref Vector3 hitPoint, ref float bestDistance) {

        if (!collision.Hit || collision.Distance >= bestDistance)
            return false;

        hitPoint = collision.Point;
        bestDistance = collision.Distance;
        return true;
    }

    private static bool TryUpdateHit(Vector3 candidatePoint, float distance, ref Vector3 hitPoint, ref float bestDistance) {

        if (distance >= bestDistance)
            return false;

        hitPoint = candidatePoint;
        bestDistance = distance;
        return true;
    }
}
