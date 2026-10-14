using System.Globalization;
using System.Text;
using GameHub.Services.Interfaces;

namespace GameHub.Services.Implementations
{
    /// <summary>
    /// Minimal, dependency-free PDF writer used for offer letters. It lays out
    /// a bold title plus a stream of body lines using the standard Helvetica
    /// Type1 fonts, wrapping long lines and paginating automatically.
    ///
    /// The project intentionally does not depend on a PDF library, so this
    /// emits a valid PDF 1.4 byte stream directly (objects, content streams,
    /// xref table, trailer).
    /// </summary>
    public class SimplePdfGenerator : ISimplePdfGenerator
    {
        private const int PageWidth = 595;
        private const int PageHeight = 842;
        private const int MarginLeft = 56;
        private const int MarginTop = 64;
        private const int TitleFontSize = 17;
        private const int BodyFontSize = 11;
        private const double BodyLeading = 15.5;
        private const int MaxCharsPerLine = 86;
        private const int LinesPerPage = 40;

        public byte[] Generate(string title, IReadOnlyList<string> bodyLines)
        {
            var wrapped = new List<string>();
            foreach (var line in bodyLines ?? Array.Empty<string>())
            {
                wrapped.AddRange(Wrap(line));
            }

            if (wrapped.Count == 0)
            {
                wrapped.Add(string.Empty);
            }

            var pages = new List<List<string>>();
            for (var index = 0; index < wrapped.Count; index += LinesPerPage)
            {
                pages.Add(wrapped.GetRange(index, Math.Min(LinesPerPage, wrapped.Count - index)));
            }

            return Build(title ?? string.Empty, pages);
        }

        private static IEnumerable<string> Wrap(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (text.Length == 0)
            {
                yield return string.Empty;
                yield break;
            }

            var current = new StringBuilder();
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidateLength = current.Length == 0 ? word.Length : current.Length + 1 + word.Length;
                if (candidateLength > MaxCharsPerLine && current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                if (word.Length > MaxCharsPerLine)
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }

                    for (var start = 0; start < word.Length; start += MaxCharsPerLine)
                    {
                        yield return word.Substring(start, Math.Min(MaxCharsPerLine, word.Length - start));
                    }

                    continue;
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(word);
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

        private static byte[] Build(string title, IReadOnlyList<List<string>> pages)
        {
            var objects = new List<byte[]>
            {
                // 1 - catalog
                Ascii("<< /Type /Catalog /Pages 2 0 R >>")
            };

            // 2 - page tree
            var kids = new StringBuilder();
            for (var index = 0; index < pages.Count; index++)
            {
                kids.Append(' ').Append(5 + (index * 2)).Append(" 0 R");
            }

            objects.Add(Ascii($"<< /Type /Pages /Kids [{kids.ToString().Trim()}] /Count {pages.Count} >>"));

            // 3 & 4 - fonts
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
            objects.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));

            for (var index = 0; index < pages.Count; index++)
            {
                var contentObjectNumber = 6 + (index * 2);
                objects.Add(Ascii(
                    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                    "/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> " +
                    $"/Contents {contentObjectNumber} 0 R >>"));

                var content = Ascii(BuildContent(title, pages[index], index == 0));
                objects.Add(Combine(
                    Ascii($"<< /Length {content.Length} >>\nstream\n"),
                    content,
                    Ascii("\nendstream")));
            }

            using var stream = new MemoryStream();
            void Write(byte[] bytes) => stream.Write(bytes, 0, bytes.Length);

            Write(Ascii("%PDF-1.4\n"));

            var offsets = new long[objects.Count + 1];
            for (var index = 0; index < objects.Count; index++)
            {
                offsets[index + 1] = stream.Position;
                Write(Ascii($"{index + 1} 0 obj\n"));
                Write(objects[index]);
                Write(Ascii("\nendobj\n"));
            }

            var xrefPosition = stream.Position;
            Write(Ascii($"xref\n0 {objects.Count + 1}\n"));
            Write(Ascii("0000000000 65535 f \n"));
            for (var index = 1; index <= objects.Count; index++)
            {
                Write(Ascii($"{offsets[index]:D10} 00000 n \n"));
            }

            Write(Ascii(
                $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n" +
                $"startxref\n{xrefPosition}\n%%EOF\n"));

            return stream.ToArray();
        }

        private static string BuildContent(string title, IReadOnlyList<string> lines, bool isFirstPage)
        {
            var text = new StringBuilder();
            var y = (double)(PageHeight - MarginTop);

            if (isFirstPage && !string.IsNullOrWhiteSpace(title))
            {
                text.Append("BT\n");
                text.Append("/F2 ").Append(TitleFontSize).Append(" Tf\n");
                text.Append(Number(MarginLeft)).Append(' ').Append(Number(y)).Append(" Td\n");
                text.Append('(').Append(Escape(title)).Append(") Tj\n");
                text.Append("ET\n");
                y -= TitleFontSize + 14;
            }

            if (lines.Count > 0)
            {
                text.Append("BT\n");
                text.Append("/F1 ").Append(BodyFontSize).Append(" Tf\n");
                text.Append(Number(BodyLeading)).Append(" TL\n");
                text.Append(Number(MarginLeft)).Append(' ').Append(Number(y)).Append(" Td\n");
                foreach (var line in lines)
                {
                    text.Append('(').Append(Escape(line)).Append(") Tj\n");
                    text.Append("T*\n");
                }

                text.Append("ET\n");
            }

            return text.ToString();
        }

        private static string Escape(string value)
        {
            var builder = new StringBuilder(value.Length + 8);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '(':
                        builder.Append("\\(");
                        break;
                    case ')':
                        builder.Append("\\)");
                        break;
                    default:
                        builder.Append(character is >= ' ' and <= '~' ? character : ' ');
                        break;
                }
            }

            return builder.ToString();
        }

        private static string Number(double value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);

        private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);

        private static byte[] Combine(params byte[][] parts)
        {
            var length = parts.Sum(part => part.Length);
            var result = new byte[length];
            var offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, result, offset, part.Length);
                offset += part.Length;
            }

            return result;
        }
    }
}
