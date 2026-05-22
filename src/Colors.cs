using Raylib_cs;

internal static class Colors {
    
    public static readonly Color Gray   = Color.Gray;
    public static readonly Color Gold   = Color.Gold;
    public static readonly Color Orange = Color.Orange;
    public static readonly Color White  = Color.White;
    public static readonly Color Ivory  = new(255, 245, 210, 255);
    
    public static readonly Color Background = new( 30,  30,  30, 255);
    public static readonly Color GridBottom = new( 40,  40,  40, 255);
    public static readonly Color GridTop    = new( 60,  60,  60, 255);
    public static readonly Color Part       = new(255, 255, 255,   5);
    public static readonly Color PartHover  = new(255, 165,   0,  10);
    public static readonly Color PartFail   = new(255,   0,   0,   5);
}
