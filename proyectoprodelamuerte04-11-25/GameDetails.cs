using System;
using System.Collections.Generic; // Añadido para GetCandidatePaths
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;
using MySql.Data.MySqlClient;

namespace proyectoprodelamuerte04_11_25
{
    public partial class GameDetails : MaterialForm
    {
        private readonly string _connectionString;
        private readonly int _gameId;
        private decimal _precio;

        // --- CONTROLES CON DISEÑO MEJORADO ---
        private PictureBox pbCover;
        private MaterialLabel lblTitulo;
        private MaterialLabel lblGenero;
        private MaterialLabel lblPrecio;
        private MaterialLabel lblLanzamiento;
        private MaterialLabel lblClasificacion;
        // Cambiado a TextBox estándar para garantizar correcta visualización del texto multiline
        private TextBox txtDescripcion;
        private TextBox txtRequisitos;
        private MaterialButton btnComprar;
        private MaterialButton btnVolver;
        private MaterialCard cardAccion;

        public GameDetails(string connectionString, int gameId)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _gameId = gameId;

            InitializeComponentCustom();

            var msm = MaterialSkinManager.Instance;
            msm.AddFormToManage(this);
            msm.Theme = MaterialSkinManager.Themes.DARK;
            msm.ColorScheme = new ColorScheme(
                Primary.BlueGrey800,
                Primary.BlueGrey900,
                Primary.BlueGrey500,
                Accent.Cyan700,
                TextShade.WHITE);

            Load += async (s, e) => await CargarJuegoAsync();
        }

        // --- MÉTODO DE DISEÑO REEMPLAZADO CON ALTURA CORREGIDA ---
        private void InitializeComponentCustom()
        {
            Text = "Detalles del juego";
            ClientSize = new Size(800, 700); // Aumentado para dar espacio a los textboxes
            StartPosition = FormStartPosition.CenterParent;

            // Portada
            pbCover = new PictureBox
            {
                Left = 24,
                Top = 80,
                Width = 220,
                Height = 300,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.None
            };

            // Título y Metadata
            lblTitulo = new MaterialLabel
            {
                Left = pbCover.Right + 30,
                Top = 80,
                Width = 480,
                Height = 30,
                Font = new Font("Roboto", 18F, FontStyle.Bold),
                Text = "Cargando Título...",
                Depth = 0
            };

            lblGenero = new MaterialLabel { Left = lblTitulo.Left, Top = lblTitulo.Bottom + 8, Width = 320, Height = 20, Text = "Género: —", Depth = 0 };
            lblLanzamiento = new MaterialLabel { Left = lblTitulo.Left, Top = lblGenero.Bottom + 6, Width = 280, Height = 20, Text = "Lanzamiento: —", Depth = 0 };
            lblClasificacion = new MaterialLabel { Left = lblTitulo.Left, Top = lblLanzamiento.Bottom + 6, Width = 400, Height = 20, Text = "Clasificación: —", Depth = 0 };

            // Card de Precio y Compra
            cardAccion = new MaterialCard
            {
                Left = lblTitulo.Left,
                Top = lblClasificacion.Bottom + 20,
                Width = 350,
                Height = 130,
                Padding = new Padding(14),
                Depth = 0
            };

            lblPrecio = new MaterialLabel
            {
                Left = 14,
                Top = 14,
                Width = 300,
                Height = 30,
                Font = new Font("Roboto", 16F, FontStyle.Bold),
                Text = "Precio: —",
                Depth = 0
            };

            btnComprar = new MaterialButton
            {
                Text = "COMPRAR",
                Type = MaterialButton.MaterialButtonType.Contained,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                AutoSize = true,
                UseAccentColor = true,
                Left = cardAccion.Width - 140,
                Top = 70,
                Enabled = false,
            };
            btnComprar.Click += async (s, e) => await BtnComprar_ClickAsync();
            cardAccion.Controls.Add(lblPrecio);
            cardAccion.Controls.Add(btnComprar);

            // Descripción y Requisitos (TEXTBOX estándar ahora, multiline visible)
            txtDescripcion = new TextBox
            {
                Left = 24,
                Top = pbCover.Bottom + 30,
                Width = ClientSize.Width - 50,
                Height = 120, // <<< ALTURA CORREGIDA
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font(FontFamily.GenericSansSerif, 11F),
                BorderStyle = BorderStyle.FixedSingle
            };

            txtRequisitos = new TextBox
            {
                Left = 24,
                Top = txtDescripcion.Bottom + 20,
                Width = ClientSize.Width - 50,
                Height = 120, // <<< ALTURA CORREGIDA
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font(FontFamily.GenericSansSerif, 11F),
                BorderStyle = BorderStyle.FixedSingle
            };

            // Botón Volver
            btnVolver = new MaterialButton
            {
                Text = "Volver",
                Type = MaterialButton.MaterialButtonType.Outlined,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 100, 48)
            };
            btnVolver.Click += (s, e) => Close();

            // Añadir controles al formulario
            Controls.Add(pbCover);
            Controls.Add(lblTitulo);
            Controls.Add(lblGenero);
            Controls.Add(lblLanzamiento);
            Controls.Add(lblClasificacion);
            Controls.Add(cardAccion);
            Controls.Add(txtDescripcion);
            Controls.Add(txtRequisitos);
            Controls.Add(btnVolver);
        }

        // --- TU LÓGICA DE CARGA DE IMAGEN (INTACTA) ---
        private IEnumerable<string> GetCandidatePaths(string portadaPath)
        {
            if (string.IsNullOrWhiteSpace(portadaPath)) yield break;
            var normalized = portadaPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized))
            {
                yield return normalized;
            }
            else
            {
                yield return Path.Combine(Application.StartupPath, normalized);
                yield return Path.Combine(AppContext.BaseDirectory, normalized);
                yield return Path.Combine(Directory.GetCurrentDirectory(), normalized);
                yield return normalized;
            }
        }

        // --- TU LÓGICA DE CARGA DE JUEGO (INTACTA) ---
        private async Task CargarJuegoAsync()
        {
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                const string sql = @"
                        SELECT ID, Titulo, Genero, Precio, Lanzamiento, Clasificacion, Requisitos, Descripcion, PortadaPath
                        FROM juego
                        WHERE ID = @id
                        LIMIT 1";

                await using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", _gameId);

                await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                if (!await reader.ReadAsync())
                {
                    MessageBox.Show("Juego no encontrado.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                    return;
                }

                lblTitulo.Text = reader["Titulo"]?.ToString() ?? "—";
                lblGenero.Text = "Género: " + (reader["Genero"]?.ToString() ?? "—");

                if (reader["Precio"] != DBNull.Value && decimal.TryParse(reader["Precio"].ToString(), out var precio))
                    _precio = precio;
                else
                    _precio = 0m;

                lblPrecio.Text = _precio > 0 ? _precio.ToString("C") : "Gratis";

                if (reader["Lanzamiento"] != DBNull.Value && DateTime.TryParse(reader["Lanzamiento"].ToString(), out var fecha))
                    lblLanzamiento.Text = "Lanzamiento: " + fecha.ToString("d");
                else
                    lblLanzamiento.Text = "Lanzamiento: —";

                lblClasificacion.Text = "Clasificación: " + (reader["Clasificacion"]?.ToString() ?? "—");
                txtDescripcion.Text = reader["Descripcion"]?.ToString() ?? "";
                txtRequisitos.Text = reader["Requisitos"]?.ToString() ?? "";

                bool imageSet = false;
                var portadaPath = reader["PortadaPath"]?.ToString();
                if (!string.IsNullOrWhiteSpace(portadaPath))
                {
                    foreach (var candidate in GetCandidatePaths(portadaPath))
                    {
                        try
                        {
                            if (File.Exists(candidate))
                            {
                                using var img = Image.FromFile(candidate);
                                var bmp = new Bitmap(img);
                                try { pbCover.Image?.Dispose(); } catch { }
                                pbCover.Image = bmp;
                                imageSet = true;
                                break;
                            }
                        }
                        catch { /* ignored */ }
                    }
                }

                if (!imageSet)
                {
                    string baseFolder = Path.Combine(Application.StartupPath, "assets", "covers");
                    foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
                    {
                        var p = Path.Combine(baseFolder, $"{_gameId}{ext}");
                        try
                        {
                            if (File.Exists(p))
                            {
                                using var img = Image.FromFile(p);
                                var bmp = new Bitmap(img);
                                try { pbCover.Image?.Dispose(); } catch { }
                                pbCover.Image = bmp;
                                imageSet = true;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (!imageSet)
                {
                    try { pbCover.Image?.Dispose(); } catch { }
                    pbCover.Image = null;
                }

                txtDescripcion.BringToFront();
                txtRequisitos.BringToFront();
                txtDescripcion.Visible = true;
                txtRequisitos.Visible = true;

                await ActualizarEstadoBotonAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar detalles del juego: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        // --- TU LÓGICA DE ESTADO DEL BOTÓN (INTACTA) ---
        private async Task ActualizarEstadoBotonAsync()
        {
            btnComprar.Enabled = false;
            var usuario = SessionData.UsuarioActual;
            if (string.IsNullOrWhiteSpace(usuario))
            {
                btnComprar.Text = "INICIA SESIÓN";
                btnComprar.Enabled = false;
                return;
            }

            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                int compradorId = 0;
                decimal saldo = 0m;
                const string selectComprador = "SELECT ID, Saldo FROM comprador WHERE Usuarioo = @usuario LIMIT 1";
                await using (var cmd = new MySqlCommand(selectComprador, conn))
                {
                    cmd.Parameters.AddWithValue("@usuario", usuario);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        compradorId = Convert.ToInt32(rdr["ID"]);
                        saldo = rdr["Saldo"] != DBNull.Value ? Convert.ToDecimal(rdr["Saldo"]) : 0m;
                    }
                }

                const string checkVenta = "SELECT COUNT(*) FROM ventas WHERE ID_comprador = @idc AND ID_Juego = @idj";
                await using (var cmd2 = new MySqlCommand(checkVenta, conn))
                {
                    cmd2.Parameters.AddWithValue("@idc", compradorId);
                    cmd2.Parameters.AddWithValue("@idj", _gameId);
                    var cnt = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
                    if (cnt > 0)
                    {
                        btnComprar.Text = "YA EN TU BIBLIOTECA";
                        btnComprar.Enabled = false;
                        return;
                    }
                }

                if (_precio == 0m)
                {
                    btnComprar.Text = "DESCARGAR";
                    btnComprar.Enabled = true;
                }
                else if (_precio > saldo)
                {
                    btnComprar.Text = $"SALDO INSUFICIENTE ({_precio:C})";
                    btnComprar.Enabled = false;
                }
                else
                {
                    btnComprar.Text = $"COMPRAR ({_precio:C})";
                    btnComprar.Enabled = true;
                }
            }
            catch
            {
                btnComprar.Text = "ERROR";
                btnComprar.Enabled = false;
            }
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // GameDetails
            // 
            ClientSize = new Size(300, 300);
            Name = "GameDetails";
            Load += GameDetails_Load;
            ResumeLayout(false);

        }

        // --- TU LÓGICA DE COMPRA (INTACTA) ---
        private async Task BtnComprar_ClickAsync()
        {
            var usuario = SessionData.UsuarioActual;
            if (string.IsNullOrWhiteSpace(usuario))
            {
                MessageBox.Show("Inicia sesión para comprar.", "Acceso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int compradorId = 0;
                decimal saldo = 0m;
                await using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    const string selectComprador = "SELECT ID, Saldo FROM comprador WHERE Usuarioo = @usuario LIMIT 1";
                    await using var cmd = new MySqlCommand(selectComprador, conn);
                    cmd.Parameters.AddWithValue("@usuario", usuario);
                    await using var rdr = await cmd.ExecuteReaderAsync();
                    if (await rdr.ReadAsync())
                    {
                        compradorId = Convert.ToInt32(rdr["ID"]);
                        saldo = rdr["Saldo"] != DBNull.Value ? Convert.ToDecimal(rdr["Saldo"]) : 0m;
                    }
                }

                if (_precio > 0 && saldo < _precio)
                {
                    MessageBox.Show("Saldo insuficiente.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await using var transConn = new MySqlConnection(_connectionString);
                await transConn.OpenAsync();
                using var tx = await transConn.BeginTransactionAsync();
                try
                {
                    if (_precio > 0)
                    {
                        const string updateSaldo = "UPDATE comprador SET Saldo = Saldo - @precio WHERE ID = @id";
                        await using (var cmdUpd = new MySqlCommand(updateSaldo, transConn, tx))
                        {
                            cmdUpd.Parameters.AddWithValue("@precio", _precio);
                            cmdUpd.Parameters.AddWithValue("@id", compradorId);
                            await cmdUpd.ExecuteNonQueryAsync();
                        }
                    }

                    const string insertVenta = @"INSERT INTO ventas (ID_comprador, ID_Juego, Fecha_Venta, Cantidad, Precio_Unitario, Descuento, Metodo_pago)
                                                 VALUES (@idc, @idj, NOW(), 1, @precio, 0.00, @mp)";
                    await using (var cmdIns = new MySqlCommand(insertVenta, transConn, tx))
                    {
                        cmdIns.Parameters.AddWithValue("@idc", compradorId);
                        cmdIns.Parameters.AddWithValue("@idj", _gameId);
                        cmdIns.Parameters.AddWithValue("@precio", _precio);
                        cmdIns.Parameters.AddWithValue("@mp", _precio > 0 ? "Saldo" : "Gratis");
                        await cmdIns.ExecuteNonQueryAsync();
                    }

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                MessageBox.Show("Compra realizada correctamente.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await ActualizarEstadoBotonAsync();
                foreach (Form open in Application.OpenForms)
                {
                    if (open is Biblioteca b)
                    {
                        try
                        {

                            await b.CargarBibliotecaAsync();
                            await b.CargarSaldoEnLabelAsync(SessionData.UsuarioActual);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error en compra: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GameDetails_Load(object sender, EventArgs e)
        {

        }
    }
}