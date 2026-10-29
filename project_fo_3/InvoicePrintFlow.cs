using System;
using System.Drawing;
using System.Linq;

namespace project_fo_3
{
    // Captured once per job; changing the application's language cannot mirror later pages.
    internal sealed class InvoicePrintFlow
    {
        internal bool LeftToRight { get; private set; }
        internal InvoicePrintFlow(bool leftToRight) { LeftToRight = leftToRight; }
        internal string Text(string arabic, string english) { return LeftToRight ? english : arabic; }
        internal StringFormat TextFormat()
        {
            return new StringFormat { Alignment = StringAlignment.Near,
                FormatFlags = LeftToRight ? 0 : StringFormatFlags.DirectionRightToLeft };
        }

        // Rectangles retain semantic indices (number, medicine, price, ..., total).
        // Only their physical coordinates change with the language.
        internal RectangleF[] Columns(float x, float y, float width, float height, params float[] weights)
        {
            if (width <= 0 || weights == null || weights.Length == 0 || weights.Any(w => w <= 0))
                throw new ArgumentOutOfRangeException(nameof(weights));
            float sum = weights.Sum(), used = 0;
            var result = new RectangleF[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                float cellWidth = i == weights.Length - 1 ? width - used : width * weights[i] / sum;
                float cellX = LeftToRight ? x + used : x + width - used - cellWidth;
                result[i] = new RectangleF(cellX, y, cellWidth, height);
                used += cellWidth;
            }
            return result;
        }

        private float CaptionWidth(Graphics graphics, string caption, Font font, float width, float preferred)
        { return Math.Min(Math.Max(preferred, graphics.MeasureString(caption, font).Width + 6), width / 2); }

        private StringFormat ValueFormat(bool valueLeftToRight)
        {
            return valueLeftToRight ? new StringFormat { Alignment = LeftToRight ? StringAlignment.Near : StringAlignment.Far }
                : TextFormat();
        }

        internal float MeasureFieldHeight(Graphics graphics, string caption, string value, Font labelFont,
            Font valueFont, float width, float labelWidth = 90, bool valueLeftToRight = false)
        {
            float captionWidth = CaptionWidth(graphics, caption, labelFont, width, labelWidth);
            using (var captionFormat = TextFormat())
            using (var valueFormat = ValueFormat(valueLeftToRight))
                return (float)Math.Ceiling(Math.Max(Math.Max(labelFont.GetHeight(graphics), valueFont.GetHeight(graphics)),
                    Math.Max(graphics.MeasureString(caption, labelFont, new SizeF(Math.Max(1, captionWidth), 100000), captionFormat).Height,
                        graphics.MeasureString(value ?? string.Empty, valueFont, new SizeF(Math.Max(1, width - captionWidth), 100000), valueFormat).Height)));
        }

        internal void Field(Graphics graphics, string caption, string value, Font labelFont, Font valueFont,
            Brush brush, RectangleF bounds, float labelWidth = 90, bool valueLeftToRight = false)
        {
            float captionWidth = CaptionWidth(graphics, caption, labelFont, bounds.Width, labelWidth);
            float valueWidth = bounds.Width - captionWidth;
            var label = new RectangleF(LeftToRight ? bounds.Left : bounds.Right - captionWidth,
                bounds.Top, captionWidth, bounds.Height);
            var content = new RectangleF(LeftToRight ? bounds.Left + captionWidth : bounds.Left,
                bounds.Top, valueWidth, bounds.Height);
            using (var format = TextFormat())
            {
                graphics.DrawString(caption, labelFont, brush, label, format);
                using (var valueFormat = ValueFormat(valueLeftToRight))
                    graphics.DrawString(value ?? string.Empty, valueFont, brush, content, valueFormat);
            }
        }
    }
}
