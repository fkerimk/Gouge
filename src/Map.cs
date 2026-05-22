internal class Map {

    public readonly List<Part> Parts = [];

    public Map Clone() {

        var map = new Map();

        foreach (var part in Parts)
            map.Parts.Add(part.Clone());

        return map;
    }

    public void CopyFrom(Map other) {
        Parts.Clear();

        foreach (var part in other.Parts)
            Parts.Add(part.Clone());
    }

    public bool ContentEquals(Map other) {

        if (ReferenceEquals(this, other))
            return true;

        if (Parts.Count != other.Parts.Count)
            return false;

        for (var i = 0; i < Parts.Count; i++) {
            var part = Parts[i];
            var otherPart = other.Parts[i];

            if (Math.Abs(part.Height - otherPart.Height) > float.Epsilon || Math.Abs(part.YOffset - otherPart.YOffset) > float.Epsilon)
                return false;

            if (!SurfaceEquals(part.Floor, otherPart.Floor) || !SurfaceEquals(part.Wall, otherPart.Wall) || !SurfaceEquals(part.Ceil, otherPart.Ceil))
                return false;

            if (part.Vertices.Count != otherPart.Vertices.Count)
                return false;

            if (part.Vertices.Where((t, j) => t != otherPart.Vertices[j]).Any()) {
                return false;
            }
        }

        return true;
    }

    private static bool SurfaceEquals(Surface left, Surface right) =>
        left.Texture == right.Texture
        && left.Mode == right.Mode
        && left.Wrap == right.Wrap
        && left.Tiling == right.Tiling
        && left.Offset == right.Offset;
}
