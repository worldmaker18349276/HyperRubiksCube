using Microsoft.Maui.Graphics;

namespace Graphics;

public class GraphicsDrawable : IDrawable
{
    void IDrawable.Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = Colors.Red;
        canvas.StrokeSize = 1;
        canvas.DrawLine(10, 10, 90, 100);
    }
}

