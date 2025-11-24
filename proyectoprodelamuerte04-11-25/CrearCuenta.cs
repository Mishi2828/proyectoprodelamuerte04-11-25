using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;
using MySql.Data.MySqlClient; // Librería MySQL

namespace proyectoprodelamuerte04_11_25
{
    public partial class CrearCuenta : MaterialForm
    {
        string conectionstring = "Server=localhost;Database=videojuegos;Uid=root;Pwd=root;";

        public CrearCuenta()
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

        // BOTÓN CREAR CUENTA
        private void materialButton1_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. RECOLECCIÓN DE DATOS
                string UsuarioVS = materialTextBox1.Text.Trim();
                string EmailVS = materialTextBox3.Text.Trim();
                string ContraVS = materialTextBox2.Text;
                string ContraVS2 = materialTextBox4.Text;
                string NombreVS = materialTextBox5.Text.Trim();
                string ApellidoVS = materialTextBox6.Text.Trim();
                string DireccionVS = materialTextBox8.Text.Trim();

                // --- TRUCO: LIMPIEZA DEL TELÉFONO ---
                // El usuario puede escribir "871-123", nosotros le quitamos los guiones
                string TelefonoSucio = materialTextBox7.Text;
                string TelefonoLimpio = TelefonoSucio.Replace("-", "").Replace(" ", "").Replace("(", "").Replace(")", "");
                // -------------------------------------

                // 2. VALIDACIONES
                if (string.IsNullOrEmpty(UsuarioVS) || string.IsNullOrEmpty(ContraVS))
                {
                    MessageBox.Show("Usuario y contraseña son obligatorios.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (ContraVS != ContraVS2)
                {
                    MessageBox.Show("Las contraseñas no coinciden.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Validamos que lo que quedó sean 10 números
                if (!long.TryParse(TelefonoLimpio, out long telefonoNumerico) || TelefonoLimpio.Length != 10)
                {
                    MessageBox.Show("El teléfono debe tener 10 dígitos válidos (ej: 871-575-3215).", "Formato Incorrecto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(conectionstring))
                {
                    connection.Open();

                    // 3. VERIFICAR SI YA EXISTE EL USUARIO
                    string checkQuery = "SELECT COUNT(*) FROM comprador WHERE Usuarioo = @UsuarioVS";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@UsuarioVS", UsuarioVS);
                        int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                        if (count > 0)
                        {
                            MessageBox.Show("El nombre de usuario ya existe.", "Duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }

                    // 4. INSERTAR EN BASE DE DATOS
                    // Nota: Guardamos 'telefonoNumerico' (que es BIGINT) en lugar del texto con guiones
                    string query = @"INSERT INTO comprador 
                                   (Nombre, Usuarioo, Email, Contrasena, Apellido, Telefono, Direccion, Rol, Saldo, Fecha_registro) 
                                   VALUES 
                                   (@NombreVS, @UsuarioVS, @CorreoVS, @ContraVS, @ApellidoVS, @TelefonoVS, @DireccionVS, 'user', 0, CURDATE())";

                    using (MySqlCommand commando = new MySqlCommand(query, connection))
                    {
                        commando.Parameters.AddWithValue("@NombreVS", NombreVS);
                        commando.Parameters.AddWithValue("@UsuarioVS", UsuarioVS);
                        commando.Parameters.AddWithValue("@CorreoVS", EmailVS);
                        commando.Parameters.AddWithValue("@ContraVS", ContraVS);
                        commando.Parameters.AddWithValue("@ApellidoVS", ApellidoVS);

                        // AQUÍ ESTÁ LA CLAVE: Guardamos el número limpio
                        commando.Parameters.AddWithValue("@TelefonoVS", telefonoNumerico);

                        commando.Parameters.AddWithValue("@DireccionVS", DireccionVS);

                        int filasAfectadas = commando.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            MessageBox.Show("Cuenta creada con éxito", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // Abrir el formulario 1 (Login)
                            Form1 f1 = new Form1();
                            f1.Show();
                            this.Hide();
                        }
                        else
                        {
                            MessageBox.Show("No se pudo guardar la cuenta.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al conectar: " + ex.Message, "Error Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // BOTÓN REGRESAR
        private void materialButton2_Click(object sender, EventArgs e)
        {
            Form1 f1 = new Form1();
            f1.Show();
            this.Hide();
        }

        // EVENTOS VACÍOS (Déjalos para que no falle el diseño)
        private void materialTextBox3_TextChanged(object sender, EventArgs e) { }
        private void materialLabel5_Click(object sender, EventArgs e) { }
        private void CrearCuenta_Load(object sender, EventArgs e) { }
        private void materialLabel7_Click(object sender, EventArgs e) { }
    }
}