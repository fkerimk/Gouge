using System.Numerics;
using Raylib_cs;

public class Surface(string texture) {

    public string Texture = texture;
    public TileMode Mode;
    public TextureWrap Wrap;
    public Vector2 Tiling = new(1, 1);
    public Vector2 Offset = Vector2.Zero;
}

public enum TileMode {
    
    World,
    Local,
}