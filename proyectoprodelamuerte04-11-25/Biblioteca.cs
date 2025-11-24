using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;
using MySql.Data.MySqlClient;

namespace proyectoprodelamuerte04_11_25
{
    public partial class Biblioteca : MaterialForm
    {
        // Define la cadena de conexión a tu base de datos
        private string connectionString = "Server=localhost;Database=videojuegos;Uid=root;Pwd=root;";

        public Biblioteca()
        {
            InitializeComponent();

            // Inicializar el Material Skin Manager
            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);
            materialSkinManager.Theme = MaterialSkinManager.Themes.DARK;
            materialSkinManager.ColorScheme = new ColorScheme(
                Primary.BlueGrey800,
                Primary.BlueGrey900,
                Primary.BlueGrey500,
                Accent.Cyan700,
                TextShade.WHITE);

            this.Load += async (s, e) =>
            {
                string usuarioActual = SessionData.UsuarioActual ?? string.Empty;
                var lblUser = this.Controls.Find("materialLabel1", true).FirstOrDefault() as MaterialLabel;
                if (!string.IsNullOrWhiteSpace(usuarioActual))
                {
                    if (lblUser != null)
                    {
                        lblUser.Text = $"Bienvenido, {usuarioActual}!";
                    }

                    // Si el usuario es admin, mostrar el botón para subir/crear juegos
                    EnsureAdminUploadButton();

                    await CargarSaldoEnLabelAsync(usuarioActual);
                    await CargarBibliotecaAsync();
                }
                else
                {
                    if (lblUser != null)
                    {
                        lblUser.Text = "Bienvenido";
                    }
                    // Si no hay sesión ocultar el botón admin si existe
                    var existingBtn = this.Controls.Find("btnUploadGame", true).FirstOrDefault() as MaterialButton;
                    if (existingBtn != null) existingBtn.Visible = false;
                }
            };
        }

        // Crea (si no existe) y muestra el botón de "Subir juego" sólo para administradores
        private void EnsureAdminUploadButton()
        {
            try
            {
                var isAdmin = SessionData.IsAdmin;
                var existing = this.Controls.Find("btnUploadGame", true).FirstOrDefault() as MaterialButton;
                if (!isAdmin)
                {
                    if (existing != null) existing.Visible = false;
                    return;
                }

                if (existing == null)
                {
                    var btnUpload = new MaterialButton
                    {
                        Name = "btnUploadGame",
                        Text = "Subir juego",
                        Type = MaterialButton.MaterialButtonType.Outlined,
                        AutoSize = true,
                        Anchor = AnchorStyles.Top | AnchorStyles.Right
                    };

                    // Posición: alineado a la derecha, ajustable
                    btnUpload.Location = new Point(this.ClientSize.Width - 140, 26);
                    btnUpload.Click += async (s, e) =>
                    {
                        // Abrir editor en modal; después refrescar biblioteca y saldo
                        using var dlg = new AdminGameEditor(connectionString);
                        dlg.ShowDialog(this);

                        try
                        {
                            await CargarSaldoEnLabelAsync(SessionData.UsuarioActual ?? string.Empty);
                            await CargarBibliotecaAsync();
                        }
                        catch { /* ignorar fallos de refresco */ }
                    };

                    this.Controls.Add(btnUpload);
                    btnUpload.BringToFront();
                }
                else
                {
                    existing.Visible = true;
                    existing.BringToFront();
                }
            }
            catch
            {
                // no interrumpir la carga de la biblioteca por un fallo visual
            }
        }

        public async Task CargarSaldoEnLabelAsync(string usuario)
        {
            decimal saldo = 0.00M;
            try
            {
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                string query = "SELECT Saldo FROM comprador WHERE Usuarioo = @Usuario";

                await using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@Usuario", usuario);
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    saldo = Convert.ToDecimal(result);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el saldo: {ex.Message}", "Error de Base de Datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Buscar label existente; si no existe, crear uno (MaterialLabel)
            var found = this.Controls.Find("materialLabel4", true);
            if (found.Length > 0 && found[0] is Label lblExisting)
            {
                lblExisting.Text = $"Saldo Disponible: {saldo:C}";
            }
            else
            {
                // Crear MaterialLabel dinámicamente y añadirlo al formulario (posición simple)
                var lblSaldo = new MaterialLabel
                {
                    Name = "materialLabel4",
                    Text = $"Saldo Disponible: {saldo:C}",
                    Left = 16,
                    Top = 80,
                    AutoSize = true
                };
                this.Controls.Add(lblSaldo);
                lblSaldo.BringToFront();
            }
        }

        // Public method used to refresh library after purchase
        public async Task CargarBibliotecaAsync()
        {
            // Crear o recuperar FlowLayoutPanel para mostrar juegos
            FlowLayoutPanel flow;
            var existing = this.Controls.Find("flowLibrary", true);
            if (existing.Length > 0 && existing[0] is FlowLayoutPanel fl) flow = fl;
            else
            {
                flow = new FlowLayoutPanel
                {
                    Name = "flowLibrary",
                    Left = 10,
                    Top = 300,
                    Width = this.ClientSize.Width - 20,
                    Height = this.ClientSize.Height - 320,
                    AutoScroll = true,
                    WrapContents = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
                };
                this.Controls.Add(flow);
                flow.BringToFront();
            }

            flow.Controls.Clear();

            try
            {
                await using var conn = new MySqlConnection(connectionString);
                await conn.OpenAsync();

                string sql = @"
                    SELECT v.ID, v.ID_comprador, v.ID_Juego, v.Fecha_Venta, j.Titulo, j.PortadaPath
                    FROM ventas v
                    INNER JOIN comprador c ON v.ID_comprador = c.ID
                    INNER JOIN juego j ON v.ID_Juego = j.ID
                    WHERE c.Usuarioo = @usuario
                    ORDER BY v.Fecha_Venta DESC;
                ";

                await using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@usuario", SessionData.UsuarioActual ?? string.Empty);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int juegoId = reader["ID_Juego"] != DBNull.Value ? Convert.ToInt32(reader["ID_Juego"]) : 0;
                    string titulo = reader["Titulo"]?.ToString() ?? "";
                    string portada = reader["PortadaPath"] != DBNull.Value ? reader["PortadaPath"]?.ToString() ?? "" : "";
                    DateTime fecha = reader["Fecha_Venta"] != DBNull.Value ? Convert.ToDateTime(reader["Fecha_Venta"]) : DateTime.MinValue;

                    var card = new Panel { Width = 180, Height = 260, BackColor = Color.FromArgb(30, 30, 30), Margin = new Padding(8) };

                    var pb = new PictureBox { Width = 160, Height = 160, Left = 10, Top = 8, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };

                    // Cargar imagen de forma segura para evitar bloqueo de fichero
                    if (!string.IsNullOrWhiteSpace(portada))
                    {
                        string candidate = portada;
                        if (!Path.IsPathRooted(candidate))
                            candidate = Path.Combine(Application.StartupPath, candidate.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(candidate))
                        {
                            try
                            {
                                using var imgStream = Image.FromFile(candidate);
                                pb.Image = new Bitmap(imgStream);
                            }
                            catch { /* ignorar error de imagen */ }
                        }
                    }

                    if (pb.Image == null)
                    {
                        string baseFolder = Path.Combine(Application.StartupPath, "assets", "covers");
                        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
                        {
                            string p = Path.Combine(baseFolder, $"{juegoId}{ext}");
                            if (File.Exists(p))
                            {
                                try
                                {
                                    using var imgStream = Image.FromFile(p);
                                    pb.Image = new Bitmap(imgStream);
                                }
                                catch { }
                                break;
                            }
                        }
                    }

                    var lbl = new Label { Text = titulo, ForeColor = Color.White, Left = 10, Top = 176, Width = 160, Height = 36, AutoEllipsis = true };
                    var lblFecha = new Label { Text = fecha == DateTime.MinValue ? "" : fecha.ToString("g"), ForeColor = Color.LightGray, Left = 10, Top = 212, Width = 160, Height = 20 };

                    card.Controls.Add(pb);
                    card.Controls.Add(lbl);
                    card.Controls.Add(lblFecha);
                    flow.Controls.Add(card);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar biblioteca: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void materialLabel1_Click(object sender, EventArgs e)
        {
        }

        private void materialButton1_Click(object sender, EventArgs e)
        {
            PaginaPrincipal paginaPrincipal = new PaginaPrincipal();
            paginaPrincipal.Show();
            this.Hide();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void materialButton2_Click(object sender, EventArgs e)
        {
            CONFIGURACION cONFIGURACION = new CONFIGURACION();
            cONFIGURACION.Show();
            this.Hide();
        }

        private void materialButton3_Click(object sender, EventArgs e)
        {
            PaginaPrincipal form3 = new PaginaPrincipal();
            form3.Show();
            this.Hide();
        }
    }
}