using System.Drawing;
using System.Drawing.Drawing2D;
using MouseGestureDesktop.Config;

namespace MouseGestureDesktop.Overlay;

/// <summary>Draws the gesture trajectory (trail + arrow + step ticks) into a bitmap.</summary>
internal static class TrajectoryRenderer
{
    /// <summary>
    /// Renders the trajectory band. The band bitmap is a horizontal strip; (originX, centerY) are
    /// in bitmap space, <paramref name="offset"/> is signed locked-axis travel in px.
    /// </summary>
    public static void Render(Graphics g, int originX, int centerY, int offset, GestureConfig cfg, float globalAlpha)
    {
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int alpha = (int)Math.Clamp(globalAlpha * 255f, 0, 255);
        if (alpha <= 0)
            return;

        Color baseColor = cfg.TrajectoryColor;
        int leadX = originX + offset;
        int thickness = Math.Max(2, cfg.TrajectoryThickness);

        // Trail: from origin to the leading edge, alpha tapering toward the origin.
        using (var trail = new GraphicsPath())
        {
            int left = Math.Min(originX, leadX);
            int right = Math.Max(originX, leadX);
            if (right - left >= 1)
            {
                var rect = new Rectangle(left, centerY - thickness / 2, right - left, thickness);
                using var brush = new LinearGradientBrush(
                    new Point(originX, centerY), new Point(leadX, centerY),
                    Color.FromArgb(0, baseColor),
                    Color.FromArgb(alpha, baseColor));
                g.FillRectangle(brush, rect);
            }
        }

        // Origin marker.
        using (var dot = new SolidBrush(Color.FromArgb(alpha, baseColor)))
        {
            int r = thickness;
            g.FillEllipse(dot, originX - r, centerY - r, r * 2, r * 2);
        }

        // Arrowhead at the leading edge, pointing in the travel direction.
        if (Math.Abs(offset) >= 6)
        {
            int dir = Math.Sign(offset);
            int head = thickness * 3;
            Point tip = new(leadX, centerY);
            Point a = new(leadX - dir * head, centerY - head);
            Point b = new(leadX - dir * head, centerY + head);
            using var arrow = new SolidBrush(Color.FromArgb(alpha, baseColor));
            g.FillPolygon(arrow, new[] { tip, a, b });
        }
    }
}
