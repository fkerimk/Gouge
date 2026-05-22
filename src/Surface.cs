using System.Numerics;
using Raylib_cs;

internal class Surface(string texture) {

    public string Texture = texture;
    public TileMode Mode;
    public TextureWrap Wrap;
    public Vector2 Tiling = new(1, 1);
    public Vector2 Offset = Vector2.Zero;

    public Surface Clone() => new(Texture) {
        Mode = Mode,
        Wrap = Wrap,
        Tiling = Tiling,
        Offset = Offset,
    };

    public void CopyFrom(Surface other) {
        Texture = other.Texture;
        Mode = other.Mode;
        Wrap = other.Wrap;
        Tiling = other.Tiling;
        Offset = other.Offset;
    }
}

internal enum TileMode {
    
    World,
    Local,
}
