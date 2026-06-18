namespace Mozc.Renderer;

// C++ src/renderer/coordinates(Point/Size/Rect)相当。
public readonly record struct Point(int X, int Y);

public readonly record struct Size(int Width, int Height);

// 左上(Left,Top)+幅高さ。
public readonly struct Rect
{
    public Rect(int left, int top, int width, int height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public Rect(Point origin, Size size)
        : this(origin.X, origin.Y, size.Width, size.Height)
    {
    }

    public int Left { get; }
    public int Top { get; }
    public int Width { get; }
    public int Height { get; }
    public int Right => Left + Width;
    public int Bottom => Top + Height;

    // C++ Rect::DeflateRect(l,t,r,b): 各辺を内側へ縮める。
    public Rect Deflate(int left, int top, int right, int bottom)
        => new(Left + left, Top + top, Width - left - right, Height - top - bottom);
}
