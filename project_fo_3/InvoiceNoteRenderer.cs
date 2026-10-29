using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
namespace project_fo_3 {
 internal interface IInvoiceNoteCursor { string Notes {get;} int NotesOffset {get;set;} }
 internal static class InvoiceNoteRenderer {
        // Windows bidi classes distinguish strong letters from numbers/punctuation.
        // Inspect the original paragraph, so a page continuation keeps its direction.
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetStringTypeW(uint type, string source, int length, [Out] ushort[] classes);

        internal static bool A4NoteRightToLeft(string notes, int offset)
        {
            int start = offset;
            while (start > 0 && !A4NoteSeparator(notes[start - 1])) start--;
            int end = offset;
            while (end < notes.Length && !A4NoteSeparator(notes[end])) end++;
            string paragraph = notes.Substring(start, end - start);
            if (paragraph.Length == 0) return false;
            var classes = new ushort[paragraph.Length];
            if (!GetStringTypeW(2, paragraph, paragraph.Length, classes))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            for (int i = 0; i < classes.Length; i++)
            {
                // Supplementary emoji are neutral for the Arabic/Latin note contract.
                if (char.IsSurrogate(paragraph[i])) continue;
                if (classes[i] == 1) return false;
                if (classes[i] == 2) return true;
            }
            return false;
        }

        private static bool A4NoteSeparator(char value)
        {
            return value == '\r' || value == '\n' || value == '\u2029';
        }

        private sealed class A4NoteFragment
        {
            internal string Text;
            internal bool RightToLeft;
            internal float Top, Height;
        }

        private static StringFormat A4NoteFormat(bool rightToLeft)
        {
            return new StringFormat {
                Alignment = StringAlignment.Near,
                FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.MeasureTrailingSpaces |
                    (rightToLeft ? StringFormatFlags.DirectionRightToLeft : 0)
            };
        }

        internal static float DrawA4Notes(Graphics g, IInvoiceNoteCursor job, Font font, Brush text,
            Brush background, Pen border, float x, float y, float width, float available)
        {
            var fragments = new List<A4NoteFragment>();
            float used = 0;
            int cursor = job.NotesOffset;
            while (cursor < job.Notes.Length && available - 8 - used >= font.Height)
            {
                int end = cursor;
                while (end < job.Notes.Length && !A4NoteSeparator(job.Notes[end])) end++;
                string remaining = job.Notes.Substring(cursor, end - cursor);
                bool rtl = A4NoteRightToLeft(job.Notes, cursor);
                int fitted = 0, lines;
                float height = font.Height;
                using (var format = A4NoteFormat(rtl))
                {
                    if (remaining.Length > 0)
                    {
                        var measured = g.MeasureString(remaining, font,
                            new SizeF(Math.Max(1, width - 16), available - 8 - used), format, out fitted, out lines);
                        if (fitted > 0 && fitted < remaining.Length && char.IsHighSurrogate(remaining[fitted - 1]) && char.IsLowSurrogate(remaining[fitted])) fitted--;
                        if (fitted <= 0) break;
                        height = Math.Max(font.Height, (float)Math.Ceiling(measured.Height));
                    }
                }
                fragments.Add(new A4NoteFragment { Text = remaining.Substring(0, fitted), RightToLeft = rtl, Top = used, Height = height });
                used += height;
                cursor += fitted;
                if (fitted < remaining.Length) break;
                if (cursor < job.Notes.Length)
                {
                    char delimiter = job.Notes[cursor++];
                    if (delimiter == '\r' && cursor < job.Notes.Length && job.Notes[cursor] == '\n') cursor++;
                }
            }
            if (cursor == job.NotesOffset) throw new InvalidOperationException("صفحة الطباعة لا تتسع لنص الملاحظات؛ عدّل الهوامش.");
            var rectangle = new RectangleF(x, y, width, used + 8);
            g.FillRectangle(background, rectangle); g.DrawRectangle(border, x, y, width, used + 8);
            foreach (var fragment in fragments)
                using (var format = A4NoteFormat(fragment.RightToLeft))
                    g.DrawString(fragment.Text, font, text,
                        new RectangleF(x + 8, y + 4 + fragment.Top, width - 16, fragment.Height), format);
            job.NotesOffset = cursor;
            return used + 8;
        }

 }}