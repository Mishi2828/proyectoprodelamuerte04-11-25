using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using MaterialSkin;
using MaterialSkin.Controls;
using MySql.Data.MySqlClient;

namespace proyectoprodelamuerte04_11_25
{
    public partial class PaginaPrincipal : MaterialForm
    {
        private const int ZOOM_OFFSET = 5;
        private const int TAMANO_EXTRA = 10;

        // Cadena conexión (usa la misma que en el resto del proyecto)
        private readonly string _connectionString = "Server=localhost;Database=videojuegos;Uid=root;Pwd=root;";

        // Guardamos la ubicación original sin usar Tag (evita sobres escribir Tag que usamos para Id)
        private readonly Dictionary<PictureBox, Point> _originalLocations = new();

        // ToolTip para mostrar títulos al pasar el ratón
        private readonly ToolTip _toolTip = new();

        public PaginaPrincipal()
        {
            InitializeComponent();

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.DARK;
            materialSkinManager.ColorScheme = new ColorScheme(
                Primary.BlueGrey800,
                Primary.BlueGrey900,
                Primary.BlueGrey500,
                Accent.Cyan700,
                TextShade.WHITE);

            // Hover
            AttachHoverEvents(pictureBox1);
            AttachHoverEvents(pictureBox2);
            AttachHoverEvents(pictureBox3);
            AttachHoverEvents(pictureBox4);

            pictureBox1.Cursor = Cursors.Hand;
            pictureBox2.Cursor = Cursors.Hand;
            pictureBox3.Cursor = Cursors.Hand;
            pictureBox4.Cursor = Cursors.Hand;

            // Click: handler genérico que abre GameDetails
            AttachClickEvents(pictureBox1);
            AttachClickEvents(pictureBox2);
            AttachClickEvents(pictureBox3);
            AttachClickEvents(pictureBox4);

            // Cargar datos al mostrar la forma
            this.Load += async (s, e) => await CargarYAsignarJuegosAsync();
        }

        private void AttachHoverEvents(PictureBox pb)
        {
            if (pb == null) return;
            pb.MouseEnter -= PictureBox_MouseEnter;
            pb.MouseLeave -= PictureBox_MouseLeave;
            pb.MouseEnter += PictureBox_MouseEnter;
            pb.MouseLeave += PictureBox_MouseLeave;
        }

        private void AttachClickEvents(PictureBox pb)
        {
            if (pb == null) return;
            pb.Click -= PictureBox_OpenDetails_Click;
            pb.Click += PictureBox_OpenDetails_Click;
        }

        private void PictureBox_MouseEnter(object sender, EventArgs e)
        {
            if (sender is not PictureBox pb) return;

            // Guardar la ubicación original en el diccionario (si aún no existe)
            if (!_originalLocations.ContainsKey(pb))
            {
                _originalLocations[pb] = pb.Location;
            }

            pb.Size = new Size(pb.Width + TAMANO_EXTRA, pb.Height + TAMANO_EXTRA);
            pb.Location = new Point(pb.Location.X - ZOOM_OFFSET, pb.Location.Y - ZOOM_OFFSET);
            pb.BorderStyle = BorderStyle.FixedSingle;
        }

        private void PictureBox_MouseLeave(object sender, EventArgs e)
        {
            if (sender is not PictureBox pb) return;

            if (_originalLocations.TryGetValue(pb, out var originalLocation))
            {
                // Restaurar al tamaño original (restar lo que agregamos)
                pb.Size = new Size(Math.Max(1, pb.Width - TAMANO_EXTRA), Math.Max(1, pb.Height - TAMANO_EXTRA));
                pb.Location = originalLocation;
                pb.BorderStyle = BorderStyle.None;
            }
        }

        // Handler genérico: lee Id desde Tag (preferido) o extrae dígitos del nombre del control
        private void PictureBox_OpenDetails_Click(object sender, EventArgs e)
        {
            if (sender is not PictureBox pb) return;

            int gameId = 0;
            // Intentar Tag (si lo llenas en diseñador o al cargar los datos)
            if (pb.Tag != null && int.TryParse(pb.Tag.ToString(), out var tagId))
            {
                gameId = tagId;
            }
            else
            {
                // fallback: extraer dígitos del nombre (pictureBox3 -> 3)
                var digits = new string(pb.Name.Where(char.IsDigit).ToArray());
                int.TryParse(digits, out gameId);
            }

            if (gameId <= 0)
            {
                MessageBox.Show("No se pudo identificar el juego. Asigna el Id en la propiedad Tag del PictureBox.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Abrir formulario de detalles
            using var dlg = new GameDetails(_connectionString, gameId);
            dlg.ShowDialog(this);
        }

        private void PaginaPrincipal_Load(object sender, EventArgs e)
        {
            // ahora la carga se hace desde la suscripción en el constructor
        }

        // Nuevo: crea/obtiene FlowLayoutPanel dinámico donde mostramos todos los juegos
        private FlowLayoutPanel GetOrCreateStoreFlow()
        {
            var found = this.Controls.Find("flowStore", true);
            if (found.Length > 0 && found[0] is FlowLayoutPanel fl) return fl;

            var flow = new FlowLayoutPanel
            {
                Name = "flowStore",
                Left = 10,
                Top = 120,
                Width = this.ClientSize.Width - 20,
                Height = this.ClientSize.Height - 140,
                AutoScroll = true,
                WrapContents = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            // Opcional: ocultar los pictureBox fijos si usamos el flow dinámico
            pictureBox1.Visible = pictureBox2.Visible = pictureBox3.Visible = pictureBox4.Visible = false;

            this.Controls.Add(flow);
            flow.BringToFront();
            return flow;
        }

        private async Task CargarYAsignarJuegosAsync()
        {
            // Usar flow dinámico para mostrar todos los juegos (incluye los nuevos)
            var flow = GetOrCreateStoreFlow();
            flow.Controls.Clear();

            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                // Traer todos o limitar (aquí traigo los 50 más recientes; ajusta según necesidad)
                string sql = @"
                    SELECT ID, Titulo, PortadaPath
                    FROM juego
                    ORDER BY ID DESC
                    LIMIT 50;
                ";

                await using var cmd = new MySqlCommand(sql, conn);

                var juegos = new List<(int Id, string Titulo, string PortadaPath)>();
                await using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int id = reader["ID"] != DBNull.Value ? Convert.ToInt32(reader["ID"]) : 0;
                        string titulo = reader["Titulo"]?.ToString() ?? "";
                        string portada = reader["PortadaPath"] != DBNull.Value ? reader["PortadaPath"]?.ToString() ?? "" : "";
                        if (id > 0) juegos.Add((id, titulo, portada));
                    }
                }

                string baseFolder = Path.Combine(Application.StartupPath, "assets", "covers");
                string[] exts = new[] { ".png", ".jpg", ".jpeg" };

                foreach (var juego in juegos)
                {
                    var card = CreateGameCard(juego.Id, juego.Titulo);

                    bool imageLoaded = false;
                    var tried = new List<string>();

                    if (!string.IsNullOrWhiteSpace(juego.PortadaPath))
                    {
                        var candidates = GetCandidatePaths(juego.PortadaPath).ToList();
                        foreach (var c in candidates)
                        {
                            tried.Add(c);
                            try
                            {
                                if (File.Exists(c))
                                {
                                    using var img = Image.FromFile(c);
                                    var bmp = new Bitmap(img);
                                    try { ((PictureBox)card.Controls["pb"]).Image?.Dispose(); } catch { }
                                    ((PictureBox)card.Controls["pb"]).Image = bmp;
                                    imageLoaded = true;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"PaginaPrincipal: error cargando {c} -> {ex.Message}");
                            }
                        }
                    }

                    if (!imageLoaded)
                    {
                        foreach (var ext in exts)
                        {
                            string path = Path.Combine(baseFolder, $"{juego.Id}{ext}");
                            tried.Add(path);
                            try
                            {
                                if (File.Exists(path))
                                {
                                    using var img = Image.FromFile(path);
                                    var bmp = new Bitmap(img);
                                    try { ((PictureBox)card.Controls["pb"]).Image?.Dispose(); } catch { }
                                    ((PictureBox)card.Controls["pb"]).Image = bmp;
                                    imageLoaded = true;
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"PaginaPrincipal: error fallback {path} -> {ex.Message}");
                            }
                        }
                    }

                    if (!imageLoaded)
                    {
                        // dejar sin imagen o poner placeholder
                        _toolTip.SetToolTip((PictureBox)card.Controls["pb"], $"{juego.Titulo}\n(Portada no encontrada)");
                    }

                    flow.Controls.Add(card);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar juegos: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private Panel CreateGameCard(int gameId, string titulo)
        {
            var card = new Panel
            {
                Width = 180,
                Height = 260,
                BackColor = Color.FromArgb(30, 30, 30),
                Margin = new Padding(8)
            };

            var pb = new PictureBox
            {
                Name = "pb",
                Width = 160,
                Height = 160,
                Left = 10,
                Top = 8,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand,
                Tag = gameId
            };

            // Reusar handlers existentes para hover / click
            AttachHoverEvents(pb);
            AttachClickEvents(pb);

            var lbl = new Label
            {
                Text = titulo,
                ForeColor = Color.White,
                Left = 10,
                Top = 176,
                Width = 160,
                Height = 36,
                AutoEllipsis = true
            };

            var lblFecha = new Label
            {
                Text = "", // opcional: podrías añadir fecha/otra info
                ForeColor = Color.LightGray,
                Left = 10,
                Top = 212,
                Width = 160,
                Height = 20
            };

            card.Controls.Add(pb);
            card.Controls.Add(lbl);
            card.Controls.Add(lblFecha);

            // Tooltip para el picturebox
            _toolTip.SetToolTip(pb, titulo);

            return card;
        }

        private IEnumerable<string> GetCandidatePaths(string portadaPath)
        {
            if (string.IsNullOrWhiteSpace(portadaPath)) yield break;

            // normalizar separadores
            var normalized = portadaPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            // si ya es absoluta, probarla primero
            if (Path.IsPathRooted(normalized))
            {
                yield return normalized;
            }
            else
            {
                // relativa al Application.StartupPath
                yield return Path.Combine(Application.StartupPath, normalized);

                // relativa a AppContext.BaseDirectory (por si se ejecuta distinto)
                yield return Path.Combine(AppContext.BaseDirectory, normalized);

                // relativa al directorio actual de trabajo
                yield return Path.Combine(Directory.GetCurrentDirectory(), normalized);

                // la ruta tal cual también por si la base de datos ya la guarda relativa al ejecutable
                yield return normalized;
            }
        }

        private void materialButton4_Click(object sender, EventArgs e)
        {
            CONFIGURACION cONFIGURACION = new CONFIGURACION();
            cONFIGURACION.Show();
            this.Hide();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void materialButton2_Click(object sender, EventArgs e)
        {
            Biblioteca f3 = new Biblioteca();
            f3.Show();
            this.Hide();
        }
    }
}