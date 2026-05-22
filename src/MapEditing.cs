using System.Numerics;

public static class MapEditing {

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

        public bool TryFindPointOnLine(float selectDistance, out Vector2 point, out int partIndex, out int insertIndex) {

            if (!map.TryFindLine(selectDistance, out point, out partIndex, out var startIndex, out _)) {
                insertIndex = -1;
                return false;
            }

            insertIndex = startIndex + 1;

            return true;
        }

        public bool TryFindPointOnLine(out Vector2 point, out int partIndex, out int insertIndex) =>
            map.TryFindPointOnLine(float.PositiveInfinity, out point, out partIndex, out insertIndex);

        public bool TryFindLine(float selectDistance, out Vector2 point, out int partIndex, out int startIndex, out int endIndex) {

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

                    if (!Util.TryGetNextVertexIndex(vertices, j, out var next))
                        continue;

                    var distance = Util.DistancePointToSegment(Gouge.MouseWorldPos, vertices[j], vertices[next], out var closestPoint);

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

        public bool TryFindLine(out Vector2 point, out int partIndex, out int startIndex, out int endIndex) =>
            map.TryFindLine(float.PositiveInfinity, out point, out partIndex, out startIndex, out endIndex);

        public int FindPartContaining(Vector2 point, int ignorePart = -1) {

            for (var i = map.Parts.Count - 1; i >= 0; i--) {

                if (i == ignorePart)
                    continue;

                if (Util.IsPointInPolygon(point, map.Parts[i].Vertices))
                    return i;
            }

            return -1;
        }
    }
}
