using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;
// Librerías de PDF
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts; // IMPORTANTE: Necesario para el arreglo de fuentes

namespace proyectoprodelamuerte04_11_25
{
    public static class BackupExporter
    {
        private record GameDto(int Id, string Title, string Genre, decimal Price, string Description, string Requirements, string PortadaPath, byte[]? ImageBytes);

        public static void ExportGamesToPdf(string connectionString, string outputPath)
        {
            // --- 1. ARREGLO DE FUENTES (Esto soluciona el error "No appropriate font") ---
            // Asignamos nuestro buscador de fuentes personalizado
            if (GlobalFontSettings.FontResolver == null)
            {
                GlobalFontSettings.FontResolver = new JuegoFontResolver();
            }
            // -----------------------------------------------------------------------------

            var games = new List<GameDto>();

            // 2. OBTENER DATOS
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                const string sql = @"SELECT ID, Titulo, Genero, Precio, Descripcion, Requisitos, PortadaPath FROM juego ORDER BY ID ASC;";
                using (var cmd = new MySqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader["ID"] != DBNull.Value ? Convert.ToInt32(reader["ID"]) : 0;
                        string title = reader["Titulo"]?.ToString() ?? "";
                        string genre = reader["Genero"]?.ToString() ?? "";
                        decimal price = reader["Precio"] != DBNull.Value ? Convert.ToDecimal(reader["Precio"]) : 0m;
                        string desc = reader["Descripcion"]?.ToString() ?? "";
                        string req = reader["Requisitos"]?.ToString() ?? "";
                        string portada = reader["PortadaPath"]?.ToString() ?? "";

                        var img = TryLoadImageBytes(portada, id);
                        games.Add(new GameDto(id, title, genre, price, desc, req, portada, img));
                    }
                }
            }

            // 3. CREAR PDF
            using (PdfDocument document = new PdfDocument())
            {
                document.Info.Title = "Respaldo de Juegos";

                // Definimos las fuentes (Ahora sí funcionarán)
                XFont fontTitle = new XFont("Arial", 14, XFontStyleEx.Bold);
                XFont fontHeader = new XFont("Arial", 16, XFontStyleEx.Bold);
                XFont fontNormal = new XFont("Arial", 10, XFontStyleEx.Regular);
                XFont fontSmall = new XFont("Arial", 8, XFontStyleEx.Regular);
                XPen linePen = new XPen(XColors.LightGray, 1);

                PdfPage page = document.AddPage();
                XGraphics gfx = XGraphics.FromPdfPage(page);

                double margin = 40;
                double yPoint = margin;
                double pageHeight = page.Height.Point;

                // Título del reporte
                gfx.DrawString($"Respaldo de juegos — {DateTime.Now:yyyy-MM-dd}", fontHeader, XBrushes.Black, margin, yPoint);
                yPoint += 40;

                foreach (var g in games)
                {
                    double rowHeight = 110;

                    // Salto de página si se acaba el espacio
                    if (yPoint + rowHeight > pageHeight - margin)
                    {
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        yPoint = margin;
                    }

                    // --- IMAGEN ---
                    double imageSize = 90;
                    if (g.ImageBytes != null && g.ImageBytes.Length > 0)
                    {
                        try
                        {
                            using (var ms = new MemoryStream(g.ImageBytes))
                            {
                                XImage xImage = XImage.FromStream(ms);
                                gfx.DrawImage(xImage, margin, yPoint, imageSize, imageSize);
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        gfx.DrawRectangle(XBrushes.LightGray, margin, yPoint, imageSize, imageSize);
                    }

                    // --- TEXTOS ---
                    double textX = margin + imageSize + 10;

                    gfx.DrawString(g.Title, fontTitle, XBrushes.Black, textX, yPoint + 15);
                    gfx.DrawString($"Género: {g.Genre} | Precio: {g.Price:C}", fontNormal, XBrushes.DarkGray, textX, yPoint + 35);

                    string shortDesc = g.Description.Length > 75 ? g.Description.Substring(0, 75) + "..." : g.Description;
                    gfx.DrawString(shortDesc, fontNormal, XBrushes.Black, textX, yPoint + 55);

                    gfx.DrawString($"ID: {g.Id}", fontSmall, XBrushes.Gray, textX, yPoint + 75);

                    // Línea separadora
                    yPoint += rowHeight;
                    gfx.DrawLine(linePen, margin, yPoint, page.Width.Point - margin, yPoint);
                    yPoint += 10;
                }

                document.Save(outputPath);
            }
        }

        private static byte[]? TryLoadImageBytes(string portadaPath, int id)
        {
            try
            {
                string rutaBase = AppDomain.CurrentDomain.BaseDirectory;
                var intentos = new List<string>();
                if (!string.IsNullOrWhiteSpace(portadaPath)) intentos.Add(portadaPath);

                intentos.Add(Path.Combine(rutaBase, "assets", "covers", $"{id}.jpg"));
                intentos.Add(Path.Combine(rutaBase, "assets", "covers", $"{id}.png"));

                foreach (var ruta in intentos)
                    if (File.Exists(ruta)) return File.ReadAllBytes(ruta);
            }
            catch { }
            return null;
        }
    }

    // ==========================================================
    // CLASE ESPECIAL PARA ARREGLAR EL PROBLEMA DE LA FUENTE ARIAL
    // ==========================================================
    public class JuegoFontResolver : IFontResolver
    {
        public string DefaultFontName => "Arial";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Si piden Arial en Negrita, usamos nuestra clave "ArialBold"
            if (familyName.Equals("Arial", StringComparison.OrdinalIgnoreCase))
            {
                if (isBold) return new FontResolverInfo("ArialBold");
                return new FontResolverInfo("ArialNormal");
            }
            // Si piden cualquier otra cosa, le damos Arial Normal para que no truene
            return new FontResolverInfo("ArialNormal");
        }

        public byte[] GetFont(string faceName)
        {
            // Aquí le decimos dónde están los archivos físicos en Windows
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

            if (faceName == "ArialBold")
            {
                string path = Path.Combine(folder, "arialbd.ttf"); // Archivo de Arial Negrita
                if (File.Exists(path)) return File.ReadAllBytes(path);
            }

            // Por defecto Arial Normal
            string pathNormal = Path.Combine(folder, "arial.ttf");
            if (File.Exists(pathNormal)) return File.ReadAllBytes(pathNormal);

            throw new FileNotFoundException("No se encontró la fuente Arial en C:\\Windows\\Fonts");
        }
    }
}