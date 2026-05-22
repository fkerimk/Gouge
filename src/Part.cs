using System.Numerics;

public class Part {

    public readonly List<Vector2> Vertices = [];
    
    public float Height = 5;
    public float YOffset = 0;
    
    public readonly Surface Floor = new("grid_orange.png");
    public readonly Surface Wall = new("grid_darkgray.png");
    public readonly Surface Ceil = new("grid_gray.png");
}