using System.Numerics;

internal static class MapEditing {

    extension(Map map) {
        
        private void DeleteVertex(int part, int vertex) {

            if (map.Parts[part].Vertices.Count <= 3) {
                map.Parts.RemoveAt(part);
                return;
            }

            map.Parts[part].Vertices.RemoveAt(vertex);
        }

        public void DeleteVertex((int part, int vertex) tuple) => map.DeleteVertex(tuple.part, tuple.vertex);

        public void InsertVertex(int part, int index, Vector2 point) {

            map.Parts[part].Vertices.Insert(index, point);
        }

        private bool TryFindLine(Vector2 targetPoint, float selectDistance, out Vector2 point, out int partIndex, out int startIndex, out int endIndex) {

            point = Vector2.Zero;
            partIndex = -1;
            startIndex = -1;
            endIndex = -1;
            var bestDistance = float.PositiveInfinity;

            for (var i = 0; i < map.Parts.Count; i++) {

                var vertices = map.Parts[i].Vertices;

                if (vertices.Count < 2)
                    continue;

                for (var j = 0; j < vertices.Count; j++) {
                    var next = Geometry2D.GetNextLoopIndex(j, vertices.Count);

                    var distance = Geometry2D.DistancePointToSegment(targetPoint, vertices[j], vertices[next], out var closestPoint);

                    if (distance > selectDistance || distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    point = closestPoint;
                    partIndex = i;
                    startIndex = j;
                    endIndex = next;
                }
            }

            return partIndex != -1;
        }

        public bool TryFindLine(Vector2 targetPoint, out Vector2 point, out int partIndex, out int startIndex, out int endIndex) =>
            map.TryFindLine(targetPoint, float.PositiveInfinity, out point, out partIndex, out startIndex, out endIndex);

        public int FindPartContaining(Vector2 point, int ignorePart = -1) {

            var bestIndex = -1;
            var bestArea = float.PositiveInfinity;

            for (var i = 0; i < map.Parts.Count; i++) {

                if (i == ignorePart)
                    continue;

                if (!Geometry2D.IsPointInPolygonOrOnEdge(point, map.Parts[i].Vertices))
                    continue;

                var area = MathF.Abs(Geometry2D.SignedArea(map.Parts[i].Vertices));

                if (area >= bestArea)
                    continue;

                bestArea = area;
                bestIndex = i;
            }

            return bestIndex;
        }
    }
}
