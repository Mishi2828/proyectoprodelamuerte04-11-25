using System;
using System.Windows.Forms;
using MaterialSkin;
using MySql.Data.MySqlClient;
using MaterialSkin.Controls;
using System.IO;

namespace proyectoprodelamuerte04_11_25
{
    public partial class Form1 : MaterialForm
    {
        string conectionstring = "Server=localhost;Database=videojuegos;Uid=root;Pwd=root;";

        public Form1()
        {
            InitializeComponent();

            var materialSkinManager = MaterialSkin.MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);

            materialSkinManager.Theme = MaterialSkin.MaterialSkinManager.Themes.DARK;

            materialSkinManager.ColorScheme = new ColorScheme(
                Primary.BlueGrey800,
                Primary.BlueGrey900,
                Primary.BlueGrey500,
                Accent.Cyan700,
                TextShade.WHITE);
        }

        private void materialButton1_Click(object sender, EventArgs e)
        {
            // Guardar usuario y contraseña
            string UsuarioVS = materialTextBox1.Text?.Trim() ?? string.Empty;
            string ContraVS = materialTextBox2.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(UsuarioVS) || string.IsNullOrWhiteSpace(ContraVS))
            {
                MessageBox.Show("Introduce usuario y contraseña.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var connection = new MySqlConnection(conectionstring))
            {
                connection.Open();

                string query = "SELECT ID, Saldo, Rol FROM comprador WHERE Usuarioo = @UsuarioVS AND Contrasena = @contraVS LIMIT 1";
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UsuarioVS", UsuarioVS);
                    command.Parameters.AddWithValue("@ContraVS", ContraVS);

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            MessageBox.Show("Bienvenido", "Acceso correcto", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            SessionData.UsuarioActual = UsuarioVS;
                            var rol = reader["Rol"]?.ToString() ?? "user";
                            SessionData.IsAdmin = string.Equals(rol, "admin", StringComparison.OrdinalIgnoreCase);

                            // Abrir siempre Biblioteca; si es admin, Biblioteca mostrará el botón "Subir juego"
                            var biblioteca = new Biblioteca();
                            biblioteca.Show();
                            this.Hide();
                        }
                        else
                        {
                            MessageBox.Show("Usuario o contraseña incorrecta", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private void materialButton2_Click(object sender, EventArgs e)
        {
            var crear = new CrearCuenta();
            crear.Show();
            this.Hide();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void materialLabel3_Click(object sender, EventArgs e)
        {
        }

        private void materialButtonExportPdf_Click(object sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"respaldo_juegos_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                BackupExporter.ExportGamesToPdf(conectionstring, sfd.FileName);
                MessageBox.Show($"PDF generado: {Path.GetFileName(sfd.FileName)}", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar PDF: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}