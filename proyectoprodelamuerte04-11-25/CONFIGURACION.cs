using MaterialSkin;
using MaterialSkin.Controls;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace proyectoprodelamuerte04_11_25
{
    public partial class CONFIGURACION : MaterialForm
    {
        string conectionstring = "Server=localhost;Database=videojuegos;Uid=root;Pwd=root;";

        public CONFIGURACION()
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
        }

        private async void BtnExportPdf_Click(object sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"respaldo_juegos_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                Title = "Guardar Respaldo PDF"
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                // Usamos Task.Run para que la ventana no se congele mientras crea el PDF
                await Task.Run(() => BackupExporter.ExportGamesToPdf(conectionstring, sfd.FileName));

                MessageBox.Show($"PDF generado correctamente:\n{Path.GetFileName(sfd.FileName)}",
                    "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar PDF. Cierra el archivo si lo tienes abierto.\n\nDetalle: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void materialButton6_Click(object sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                // Filtro para que lo guarde como TXT (Bloc de notas) o SQL
                Filter = "Archivo de Texto (*.txt)|*.txt|Archivo SQL (*.sql)|*.sql",
                FileName = $"respaldo_normal_{DateTime.Now:yyyyMMdd_HHmm}.txt",
                Title = "Guardar Respaldo en Bloc de Notas"
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                // Ejecutamos la generación del archivo en segundo plano
                await Task.Run(() => GenerarArchivoSQL(sfd.FileName));

                MessageBox.Show($"Respaldo creado correctamente.\nPuedes abrirlo con el Bloc de Notas.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear respaldo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Método auxiliar que escribe los datos en el archivo de texto
        private void GenerarArchivoSQL(string rutaArchivo)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- RESPALDO DE BASE DE DATOS: videojuegos (Tabla Juego)");
            sb.AppendLine($"-- FECHA DE CREACIÓN: {DateTime.Now}");
            sb.AppendLine("-- Este archivo se puede abrir con el Bloc de Notas");
            sb.AppendLine("-- ------------------------------------------------------");
            sb.AppendLine();

            using (var conn = new MySqlConnection(conectionstring))
            {
                conn.Open();

                // Respaldar Tabla JUEGOS
                string sql = "SELECT * FROM juego";
                using (var cmd = new MySqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    sb.AppendLine("-- Borrar datos antiguos antes de insertar");
                    sb.AppendLine("DELETE FROM juego;");
                    sb.AppendLine();
                    sb.AppendLine("-- Insertar datos guardados");

                    while (reader.Read())
                    {
                        // Obtener datos y limpiar caracteres especiales para que no falle
                        var id = reader["ID"].ToString();
                        var titulo = reader["Titulo"].ToString()?.Replace("'", "''");
                        var genero = reader["Genero"].ToString()?.Replace("'", "''");
                        var precio = reader["Precio"].ToString().Replace(",", ".");
                        var desc = reader["Descripcion"].ToString()?.Replace("'", "''");
                        var req = reader["Requisitos"].ToString()?.Replace("'", "''");
                        var port = reader["PortadaPath"].ToString()?.Replace("\\", "\\\\");

                        // Crear la línea de texto
                        sb.AppendLine($"INSERT INTO juego (ID, Titulo, Genero, Precio, Descripcion, Requisitos, PortadaPath) " +
                                      $"VALUES ({id}, '{titulo}', '{genero}', {precio}, '{desc}', '{req}', '{port}');");
                    }
                }
            }

            // Guardar todo el texto en el archivo seleccionado
            File.WriteAllText(rutaArchivo, sb.ToString());
        }

        private void materialButton1_Click(object sender, EventArgs e)
        {
            PaginaPrincipal paginaPrincipal = new PaginaPrincipal();
            paginaPrincipal.Show();
            this.Hide();
        }

        private void materialButton2_Click(object sender, EventArgs e)
        {
            Biblioteca f3 = new Biblioteca();
            f3.Show();
            this.Hide();
        }

        private void materialButton3_Click(object sender, EventArgs e)
        {
            // Validar que sea un número
            if (!decimal.TryParse(materialTextBox1.Text, out decimal saldo))
            {
                MessageBox.Show("Por favor ingresa un número válido.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (MySqlConnection connection = new MySqlConnection(conectionstring))
            {
                connection.Open();
                string query = "UPDATE comprador SET Saldo = Saldo + @saldo WHERE Usuarioo = @usuarioActual";
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@saldo", saldo);
                    command.Parameters.AddWithValue("@usuarioActual", SessionData.UsuarioActual);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        MessageBox.Show($"Se agregaron ${saldo} a la cuenta.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        materialTextBox1.Clear();
                    }
                    else
                    {
                        MessageBox.Show("No se pudo actualizar. Verifica que hayas iniciado sesión.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void materialButton4_Click(object sender, EventArgs e)
        {
            string usuarioVSNuevo = materialTextBox2.Text;
            MessageBox.Show($"Lógica para cambiar usuario a: {usuarioVSNuevo} (Pendiente)");
        }

        private void materialButton5_Click(object sender, EventArgs e)
        {
            string contrasenaVSNueva = materialTextBox3.Text;
            MessageBox.Show("Lógica para cambiar contraseña (Pendiente)");
        }
    }
}