using UnityEngine;

namespace IrisMenus.UI
{
    public enum UiGlyphKind { None, Search, Close, Sun, Moon, Plus, ArrowRight, ChevronRight, Help, Menu, Settings, Layers, Grid, Check, Reset }

    public static class UiGlyph
    {
        public static void Draw(UiContext context, Rect rect, UiGlyphKind kind, Color color, float rotation = 0f)
        {
            if (!context.IsRepaint || rect.width <= 0f || rect.height <= 0f || kind == UiGlyphKind.None) return;
            float size = Mathf.Min(rect.width, rect.height);
            float stroke = size * 0.085f;
            Vector2 center = rect.center;
            float radians = rotation * Mathf.Deg2Rad, cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            Vector2 Point(float x, float y)
            {
                x *= size; y *= size;
                return center + new Vector2(x * cosine - y * sine, x * sine + y * cosine);
            }
            void Line(float x1, float y1, float x2, float y2) => context.Line(Point(x1, y1), Point(x2, y2), color, stroke);
            void Arc(float x, float y, float radius, float from, float to, int segments)
            {
                Vector2 previous = Point(x + Mathf.Cos(from) * radius, y + Mathf.Sin(from) * radius);
                for (int i = 1; i <= segments; i++)
                {
                    float angle = UiMotion.Lerp(from, to, i / (float)segments);
                    Vector2 next = Point(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
                    context.Line(previous, next, color, stroke);
                    previous = next;
                }
            }
            switch (kind)
            {
                case UiGlyphKind.Search:
                    Arc(-0.08f, -0.08f, 0.29f, 0f, Mathf.PI * 2f, 18);
                    Line(0.14f, 0.14f, 0.39f, 0.39f);
                    break;
                case UiGlyphKind.Close:
                    Line(-0.28f, -0.28f, 0.28f, 0.28f); Line(0.28f, -0.28f, -0.28f, 0.28f);
                    break;
                case UiGlyphKind.Sun:
                    Arc(0f, 0f, 0.19f, 0f, Mathf.PI * 2f, 16);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI * 0.25f;
                        Line(Mathf.Cos(angle) * 0.31f, Mathf.Sin(angle) * 0.31f, Mathf.Cos(angle) * 0.43f, Mathf.Sin(angle) * 0.43f);
                    }
                    break;
                case UiGlyphKind.Moon:
                    Arc(0f, 0f, 0.37f, -0.55f, 4.35f, 18);
                    Arc(0.22f, -0.25f, 0.31f, 1.08f, 3.98f, 12);
                    break;
                case UiGlyphKind.Plus:
                    Line(-0.32f, 0f, 0.32f, 0f); Line(0f, -0.32f, 0f, 0.32f);
                    break;
                case UiGlyphKind.ArrowRight:
                    Line(-0.35f, 0f, 0.33f, 0f); Line(0.04f, -0.28f, 0.33f, 0f); Line(0.33f, 0f, 0.04f, 0.28f);
                    break;
                case UiGlyphKind.ChevronRight:
                    Line(-0.13f, -0.28f, 0.15f, 0f); Line(0.15f, 0f, -0.13f, 0.28f);
                    break;
                case UiGlyphKind.Help:
                    Arc(0f, 0f, 0.4f, 0f, Mathf.PI * 2f, 24);
                    Arc(0f, -0.1f, 0.12f, Mathf.PI, Mathf.PI * 2.3f, 10);
                    Line(0.07f, -0.01f, 0f, 0.1f); Line(0f, 0.22f, 0f, 0.25f);
                    break;
                case UiGlyphKind.Menu:
                    Line(-0.35f, -0.24f, 0.35f, -0.24f); Line(-0.35f, 0f, 0.35f, 0f); Line(-0.35f, 0.24f, 0.35f, 0.24f);
                    break;
                case UiGlyphKind.Settings:
                    for (int i = 0; i < 3; i++)
                    {
                        float y = -0.3f + i * 0.3f, x = i == 1 ? 0.13f : -0.12f;
                        Line(-0.4f, y, x - 0.08f, y); Line(x + 0.08f, y, 0.4f, y);
                        Line(x - 0.08f, y - 0.1f, x - 0.08f, y + 0.1f);
                        Line(x + 0.08f, y - 0.1f, x + 0.08f, y + 0.1f);
                        Line(x - 0.08f, y - 0.1f, x + 0.08f, y - 0.1f);
                        Line(x - 0.08f, y + 0.1f, x + 0.08f, y + 0.1f);
                    }
                    break;
                case UiGlyphKind.Layers:
                    Line(0f, -0.4f, 0.4f, -0.16f); Line(0.4f, -0.16f, 0f, 0.08f);
                    Line(0f, 0.08f, -0.4f, -0.16f); Line(-0.4f, -0.16f, 0f, -0.4f);
                    Line(-0.4f, 0.08f, 0f, 0.32f); Line(0f, 0.32f, 0.4f, 0.08f);
                    break;
                case UiGlyphKind.Grid:
                    for (int row = -1; row <= 1; row++)
                        for (int column = -1; column <= 1; column++)
                        {
                            Vector2 point = Point(column * 0.26f, row * 0.26f);
                            context.Fill(new Rect(point.x - stroke, point.y - stroke, stroke * 2f, stroke * 2f), color, stroke);
                        }
                    break;
                case UiGlyphKind.Check:
                    Line(-0.34f, 0f, -0.1f, 0.25f); Line(-0.1f, 0.25f, 0.36f, -0.27f);
                    break;
                case UiGlyphKind.Reset:
                    Arc(0f, 0f, 0.34f, -2.6f, 2.9f, 20);
                    Line(-0.36f, -0.35f, -0.36f, -0.05f); Line(-0.36f, -0.05f, -0.06f, -0.05f);
                    break;
            }
        }
    }
}
