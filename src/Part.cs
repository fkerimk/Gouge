using System.Numerics;

internal class Part {

    public readonly List<Vector2> Vertices = [];
    
    public float Height = 5;
    public float YOffset;
    
    public readonly Surface Floor = new("grid_orange.png");
    public readonly Surface Wall = new("grid_darkgray.png");
    public readonly Surface Ceil = new("grid_gray.png");

    public Part Clone() {

        var part = new Part {
            Height = Height,
            YOffset = YOffset,
        };

        part.Vertices.AddRange(Vertices);
        part.Floor.CopyFrom(Floor);
        part.Wall.CopyFrom(Wall);
        part.Ceil.CopyFrom(Ceil);
        return part;
    }
}
