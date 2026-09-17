using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using CmdNext.Models.Domain.DTOs.Ai;
using OfficeOpenXml;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CmdNext.Service.Helpers
{
    // Converts PDF / DOCX / XLSX attachments to Markdown so the model can read their
    // content, and passes images through as base64 data.
    public class AiAttachmentHelper
    {
        private static readonly Dictionary<string, string> ImageMediaTypes = new()
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp"
        };

        public static AiChatMessageAttachment ToAttachment(string fileName, byte[] content)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (ImageMediaTypes.TryGetValue(extension, out var mediaType))
            {
                return new AiChatMessageAttachment
                {
                    FileName = fileName,
                    MediaType = mediaType,
                    Data = System.Convert.ToBase64String(content)
                };
            }

            return new AiChatMessageAttachment
            {
                FileName = fileName,
                Markdown = ConvertToMarkdown(extension, content)
            };
        }

        private static string ConvertToMarkdown(string extension, byte[] content)
        {
            return extension switch
            {
                ".xlsx" or ".xlsm" => ConvertExcel(content),
                ".docx" => ConvertDocx(content),
                ".pdf" => ConvertPdf(content),
                _ => throw new NotSupportedException(
                    $"Attachment type '{extension}' is not supported. Supported types: .xlsx, .xlsm, .docx, .pdf, .png, .jpg, .jpeg, .gif, .webp.")
            };
        }

        private static string ConvertExcel(byte[] content)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var stream = new MemoryStream(content);
            using var package = new ExcelPackage(stream);

            var sb = new StringBuilder();

            foreach (var sheet in package.Workbook.Worksheets)
            {
                if (sheet.Dimension == null)
                {
                    continue;
                }

                sb.AppendLine($"## {sheet.Name}");
                sb.AppendLine();

                var startRow = sheet.Dimension.Start.Row;
                var endRow = sheet.Dimension.End.Row;
                var startCol = sheet.Dimension.Start.Column;
                var endCol = sheet.Dimension.End.Column;

                var isFirstRow = true;

                for (var row = startRow; row <= endRow; row++)
                {
                    var cells = new List<string>();
                    for (var col = startCol; col <= endCol; col++)
                    {
                        cells.Add(EscapeCell(sheet.Cells[row, col].Text));
                    }

                    if (cells.All(string.IsNullOrEmpty))
                    {
                        continue;
                    }

                    sb.AppendLine("| " + string.Join(" | ", cells) + " |");

                    if (isFirstRow)
                    {
                        sb.AppendLine("|" + string.Concat(Enumerable.Repeat(" --- |", cells.Count)));
                        isFirstRow = false;
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string ConvertDocx(byte[] content)
        {
            using var stream = new MemoryStream(content);
            using var document = WordprocessingDocument.Open(stream, false);

            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            foreach (var element in body.ChildElements)
            {
                switch (element)
                {
                    case Paragraph paragraph:
                        AppendParagraph(sb, paragraph);
                        break;
                    case Table table:
                        AppendTable(sb, table);
                        break;
                }
            }

            return sb.ToString();
        }

        private static void AppendParagraph(StringBuilder sb, Paragraph paragraph)
        {
            var text = paragraph.InnerText;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;

            if (styleId != null
                && styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(styleId["Heading".Length..], out var level)
                && level is >= 1 and <= 6)
            {
                sb.AppendLine($"{new string('#', level)} {text}");
            }
            else if (paragraph.ParagraphProperties?.NumberingProperties != null)
            {
                sb.AppendLine($"- {text}");
            }
            else
            {
                sb.AppendLine(text);
            }

            sb.AppendLine();
        }

        private static void AppendTable(StringBuilder sb, Table table)
        {
            var rows = table.Elements<TableRow>().ToList();

            for (var i = 0; i < rows.Count; i++)
            {
                var cells = rows[i].Elements<TableCell>()
                    .Select(cell => EscapeCell(cell.InnerText))
                    .ToList();

                sb.AppendLine("| " + string.Join(" | ", cells) + " |");

                if (i == 0)
                {
                    sb.AppendLine("|" + string.Concat(Enumerable.Repeat(" --- |", cells.Count)));
                }
            }

            sb.AppendLine();
        }

        private static string ConvertPdf(byte[] content)
        {
            using var document = PdfDocument.Open(content);

            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var text = ContentOrderTextExtractor.GetText(page);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                sb.AppendLine(text.Trim());
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string EscapeCell(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("|", "\\|")
                .Trim();
        }
    }
}
