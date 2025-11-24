using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;
using MySql.Data.MySqlClient;

namespace proyectoprodelamuerte04_11_25
{
    public class AdminGameEditor : MaterialForm
    {
        private readonly string _connectionString;
        private readonly int? _gameId; // null => crear
        private string? _selectedImagePath;

        // Controles
        private MaterialTextBox txtTitulo;
        private MaterialTextBox txtGenero;
        private MaterialTextBox txtPrecio;
        private MaterialTextBox txtLanzamiento;
        private MaterialTextBox txtClasificacion;
        private TextBox txtRequisitos;
        private TextBox txtDescripcion;
        private MaterialButton btnSelectImage;
        private MaterialButton btnSave;
        private PictureBox pbPreview;

        public AdminGameEditor(string connectionString, int? gameId = null)
        {
            _connectionString = connectionString;
            _gameId = gameId;

            InitializeComponents();

            var msm = MaterialSkinManager.Instance;
            msm.AddFormToManage(this);
            msm.Theme = MaterialSkinManager.Themes.DARK;
            msm.ColorScheme = new ColorScheme(
                Primary.BlueGrey800,
                Primary.BlueGrey900,
                Primary.BlueGrey500,
                Accent.Cyan700,
                TextShade.WHITE);

            Load += async (s, e) =>
            {
                if (_gameId.HasValue) await LoadGameAsync(_gameId.Value);
            };
        }

        private void InitializeComponents()
        {
            Text = _gameId.HasValue ? "Editar juego" : "Nuevo juego";
            ClientSize = new Size(760, 560);
            StartPosition = FormStartPosition.CenterParent;

            txtTitulo = new MaterialTextBox { Left = 16, Top = 80, Width = 480, Hint = "Título" };
            txtGenero = new MaterialTextBox { Left = 16, Top = 130, Width = 240, Hint = "Género" };
            txtPrecio = new MaterialTextBox { Left = 260, Top = 130, Width = 120, Hint = "Precio" };
            txtLanzamiento = new MaterialTextBox { Left = 390, Top = 130, Width = 180, Hint = "Lanzamiento (YYYY-MM-DD)" };
            txtClasificacion = new MaterialTextBox { Left = 16, Top = 180, Width = 240, Hint = "Clasificación" };

            txtRequisitos = new TextBox { Left = 16, Top = 220, Width = 550, Height = 80, Multiline = true, BackColor = Color.FromArgb(40,40,40), ForeColor = Color.White };
            txtDescripcion = new TextBox { Left = 16, Top = 310, Width = 550, Height = 120, Multiline = true, BackColor = Color.FromArgb(40,40,40), ForeColor = Color.White };

            btnSelectImage = new MaterialButton { Text = "Seleccionar portada...", Left = 580, Top = 80, AutoSize = true };
            btnSelectImage.Click += BtnSelectImage_Click;

            pbPreview = new PictureBox { Left = 580, Top = 120, Width = 160, Height = 200, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };

            btnSave = new MaterialButton { Text = _gameId.HasValue ? "Actualizar" : "Crear", Left = ClientSize.Width - 140, Top = ClientSize.Height - 64, AutoSize = true };
            btnSave.Click += async (s, e) => await BtnSave_ClickAsync();

            Controls.AddRange(new Control[] { txtTitulo, txtGenero, txtPrecio, txtLanzamiento, txtClasificacion, txtRequisitos, txtDescripcion, btnSelectImage, pbPreview, btnSave });
        }

        private void BtnSelectImage_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Imágenes|*.png;*.jpg;*.jpeg";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _selectedImagePath = ofd.FileName;
                try
                {
                    pbPreview.Image?.Dispose();
                }
                catch { }
                pbPreview.Image = Image.FromFile(_selectedImagePath);
            }
        }

        private async Task LoadGameAsync(int id)
        {
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();
                const string sql = @"SELECT Titulo, Genero, Precio, Lanzamiento, Clasificacion, Requisitos, Descripcion, PortadaPath FROM juego WHERE ID = @id LIMIT 1";
                await using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", id);
                await using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
                if (await reader.ReadAsync())
                {
                    txtTitulo.Text = reader["Titulo"]?.ToString() ?? "";
                    txtGenero.Text = reader["Genero"]?.ToString() ?? "";
                    txtPrecio.Text = reader["Precio"]?.ToString() ?? "";
                    txtLanzamiento.Text = reader["Lanzamiento"]?.ToString() ?? "";
                    txtClasificacion.Text = reader["Clasificacion"]?.ToString() ?? "";
                    txtRequisitos.Text = reader["Requisitos"]?.ToString() ?? "";
                    txtDescripcion.Text = reader["Descripcion"]?.ToString() ?? "";

                    var portada = reader["PortadaPath"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(portada))
                    {
                        var candidates = GetCandidatePaths(portada);
                        foreach (var c in candidates)
                        {
                            if (File.Exists(c))
                            {
                                try { pbPreview.Image?.Dispose(); } catch { }
                                pbPreview.Image = Image.FromFile(c);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cargando juego: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private IEnumerable<string> GetCandidatePaths(string portadaPath)
        {
            if (string.IsNullOrWhiteSpace(portadaPath)) yield break;
            var normalized = portadaPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalized)) yield return normalized;
            yield return Path.Combine(Application.StartupPath, normalized);
            yield return Path.Combine(AppContext.BaseDirectory, normalized);
            yield return Path.Combine(Directory.GetCurrentDirectory(), normalized);
            yield return normalized;
        }

        private async Task BtnSave_ClickAsync()
        {
            // Validación mínima
            if (string.IsNullOrWhiteSpace(txtTitulo.Text))
            {
                MessageBox.Show("Indica el título.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                if (_gameId.HasValue)
                {
                    // UPDATE
                    const string updateSql = @"UPDATE juego SET Titulo=@t, Genero=@g, Precio=@p, Lanzamiento=@l, Clasificacion=@c, Requisitos=@r, Descripcion=@d WHERE ID = @id";
                    await using (var cmd = new MySqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@t", txtTitulo.Text);
                        cmd.Parameters.AddWithValue("@g", txtGenero.Text);
                        cmd.Parameters.AddWithValue("@p", decimal.TryParse(txtPrecio.Text, out var pr) ? pr : 0m);
                        cmd.Parameters.AddWithValue("@l", DateTime.TryParse(txtLanzamiento.Text, out var dt) ? dt : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@c", txtClasificacion.Text);
                        cmd.Parameters.AddWithValue("@r", txtRequisitos.Text);
                        cmd.Parameters.AddWithValue("@d", txtDescripcion.Text);
                        cmd.Parameters.AddWithValue("@id", _gameId.Value);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    if (!string.IsNullOrWhiteSpace(_selectedImagePath))
                    {
                        await SaveCoverAndUpdatePathAsync(conn, _gameId.Value, _selectedImagePath);
                    }

                    MessageBox.Show("Juego actualizado.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // INSERT
                    const string insertSql = @"INSERT INTO juego (Titulo, Genero, Precio, Lanzamiento, Clasificacion, Requisitos, Descripcion) 
                                               VALUES (@t,@g,@p,@l,@c,@r,@d)";
                    await using (var cmd = new MySqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@t", txtTitulo.Text);
                        cmd.Parameters.AddWithValue("@g", txtGenero.Text);
                        cmd.Parameters.AddWithValue("@p", decimal.TryParse(txtPrecio.Text, out var pr) ? pr : 0m);
                        cmd.Parameters.AddWithValue("@l", DateTime.TryParse(txtLanzamiento.Text, out var dt) ? dt : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@c", txtClasificacion.Text);
                        cmd.Parameters.AddWithValue("@r", txtRequisitos.Text);
                        cmd.Parameters.AddWithValue("@d", txtDescripcion.Text);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    // Obtener id insertado
                    await using var cmdId = new MySqlCommand("SELECT LAST_INSERT_ID()", conn);
                    var newIdObj = await cmdId.ExecuteScalarAsync();
                    int newId = Convert.ToInt32(newIdObj);

                    if (!string.IsNullOrWhiteSpace(_selectedImagePath))
                    {
                        await SaveCoverAndUpdatePathAsync(conn, newId, _selectedImagePath);
                    }

                    MessageBox.Show("Juego creado.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error guardando juego: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SaveCoverAndUpdatePathAsync(MySqlConnection conn, int gameId, string sourcePath)
        {
            // Copiar la imagen a assets/covers/{id}{ext} y guardar ruta relativa en BD
            var ext = Path.GetExtension(sourcePath);
            var destFolder = Path.Combine(Application.StartupPath, "assets", "covers");
            Directory.CreateDirectory(destFolder);
            var destName = $"{gameId}{ext}";
            var destPath = Path.Combine(destFolder, destName);

            File.Copy(sourcePath, destPath, true);

            // Ruta relativa
            var relative = Path.Combine("assets", "covers", destName).Replace('\\', '/');

            const string updatePathSql = "UPDATE juego SET PortadaPath = @p WHERE ID = @id";
            await using var cmd = new MySqlCommand(updatePathSql, conn);
            cmd.Parameters.AddWithValue("@p", relative);
            cmd.Parameters.AddWithValue("@id", gameId);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}