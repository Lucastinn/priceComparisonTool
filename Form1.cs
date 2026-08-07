using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ComparadorPrecios.Models;
using ComparadorPrecios.Services;

namespace ComparadorPrecios
{
    public partial class Form1 : Form
    {
        // Controles visuales
        private TextBox txtNombreProv1;
        private TextBox txtNombreProv2;
        private TextBox txtNombreProv3;
        private ProgressBar barraCarga;
        private Button btnCargarA;
        private Button btnCargarB;
        private Button btnCargarC;
        private Button btnComparar;
        private DataGridView dgvResultados;
        private Label lblEstado;
        private TextBox txtBuscar;

        // Servicios y datos en memoria
        private string rutaProvA, rutaProvB, rutaProvC;
        private ExcelService excelService = new ExcelService();
        private TextNormalizer normalizer = new TextNormalizer();
        private List<ProductoComparado> catalogoMaestro = new List<ProductoComparado>();

        // Guardamos los nombres actuales para usarlos en el buscador sin perderlos
        private string nombreProvA = "Prov A";
        private string nombreProvB = "Prov B";
        private string nombreProvC = "Prov C";

        public Form1()
        {
            ConfigurarInterfazGrafica();
        }

        private void ConfigurarInterfazGrafica()
        {
            this.Text = "Comparador de Precios Inteligente";
            this.Size = new Size(850, 650); // Un poquito más alto para que entre todo bien
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. Cajas de texto para los nombres de los proveedores
            txtNombreProv1 = new TextBox() { Location = new Point(20, 20), Width = 120, PlaceholderText = "Nombre Mayorista 1" };
            txtNombreProv2 = new TextBox() { Location = new Point(150, 20), Width = 120, PlaceholderText = "Nombre Mayorista 2" };
            txtNombreProv3 = new TextBox() { Location = new Point(280, 20), Width = 120, PlaceholderText = "Nombre Mayorista 3" };

            // 2. Botones de carga (los bajamos un poquito)
            btnCargarA = new Button() { Text = "Excel 1", Location = new Point(20, 45), Width = 120 };
            btnCargarB = new Button() { Text = "Excel 2", Location = new Point(150, 45), Width = 120 };
            btnCargarC = new Button() { Text = "Excel 3", Location = new Point(280, 45), Width = 120 };

            // Botón comparar más grande
            btnComparar = new Button() { Text = "COMPARAR", Location = new Point(420, 20), Width = 120, Height = 48, BackColor = Color.LightBlue, Font = new Font("Arial", 9, FontStyle.Bold) };

            // 3. Barra de carga (oculta al inicio)
            barraCarga = new ProgressBar() { Location = new Point(560, 20), Width = 250, Height = 18, Style = ProgressBarStyle.Marquee, Visible = false };

            lblEstado = new Label() { Text = "Esperando archivos...", Location = new Point(560, 45), Width = 250, ForeColor = Color.Gray };

            // Buscador (lo bajamos un poco también)
            txtBuscar = new TextBox()
            {
                Location = new Point(20, 80),
                Width = 380,
                PlaceholderText = "Escribí para buscar un producto..."
            };
            txtBuscar.TextChanged += (s, e) => CargarGrilla(txtBuscar.Text);

            // Eventos
            btnCargarA.Click += (s, e) => { rutaProvA = SeleccionarArchivo(txtNombreProv1.Text != "" ? txtNombreProv1.Text : "Proveedor 1"); ActualizarEstado(); };
            btnCargarB.Click += (s, e) => { rutaProvB = SeleccionarArchivo(txtNombreProv2.Text != "" ? txtNombreProv2.Text : "Proveedor 2"); ActualizarEstado(); };
            btnCargarC.Click += (s, e) => { rutaProvC = SeleccionarArchivo(txtNombreProv3.Text != "" ? txtNombreProv3.Text : "Proveedor 3"); ActualizarEstado(); };
            btnComparar.Click += BtnComparar_Click;

            // Tabla
            dgvResultados = new DataGridView()
            {
                Location = new Point(20, 110),
                Size = new Size(790, 480),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.WhiteSmoke
            };
            dgvResultados.CellFormatting += DgvResultados_CellFormatting;

            // Agregar todo a la ventana
            this.Controls.Add(txtNombreProv1);
            this.Controls.Add(txtNombreProv2);
            this.Controls.Add(txtNombreProv3);
            this.Controls.Add(barraCarga);
            this.Controls.Add(btnCargarA);
            this.Controls.Add(btnCargarB);
            this.Controls.Add(btnCargarC);
            this.Controls.Add(btnComparar);
            this.Controls.Add(lblEstado);
            this.Controls.Add(txtBuscar);
            this.Controls.Add(dgvResultados);
        }

        private string SeleccionarArchivo(string nombreProveedor)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = $"Seleccionar Excel de {nombreProveedor}";
                ofd.Filter = "Archivos Excel (*.xlsx)|*.xlsx";

                if (ofd.ShowDialog() == DialogResult.OK) return ofd.FileName;
            }
            return null;
        }

        private void ActualizarEstado()
        {
            int cargados = 0;
            if (rutaProvA != null) cargados++;
            if (rutaProvB != null) cargados++;
            if (rutaProvC != null) cargados++;

            lblEstado.Text = $"Archivos cargados: {cargados} de 3";
            lblEstado.ForeColor = cargados > 1 ? Color.Green : Color.Gray;
        }

        private async void BtnComparar_Click(object sender, EventArgs e)
        {
            if (rutaProvA == null && rutaProvB == null && rutaProvC == null)
            {
                MessageBox.Show("Por favor, cargá al menos un archivo Excel.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Guardamos los nombres que escribió el usuario (o un valor por defecto)
            nombreProvA = string.IsNullOrWhiteSpace(txtNombreProv1.Text) ? "Proveedor 1" : txtNombreProv1.Text;
            nombreProvB = string.IsNullOrWhiteSpace(txtNombreProv2.Text) ? "Proveedor 2" : txtNombreProv2.Text;
            nombreProvC = string.IsNullOrWhiteSpace(txtNombreProv3.Text) ? "Proveedor 3" : txtNombreProv3.Text;

            // UI Estado de carga
            btnComparar.Text = "Procesando...";
            btnComparar.Enabled = false;
            barraCarga.Visible = true; // Mostramos la barra
            catalogoMaestro.Clear();

            try
            {
                await Task.Run(() =>
                {
                    if (!string.IsNullOrEmpty(rutaProvA)) ProcesarProveedor(rutaProvA, nombreProvA);
                    if (!string.IsNullOrEmpty(rutaProvB)) ProcesarProveedor(rutaProvB, nombreProvB);
                    if (!string.IsNullOrEmpty(rutaProvC)) ProcesarProveedor(rutaProvC, nombreProvC);
                });

                CargarGrilla();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hubo un error al leer los archivos: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnComparar.Text = "COMPARAR";
                btnComparar.Enabled = true;
                barraCarga.Visible = false; // Ocultamos la barra al terminar
            }
        }

        private void ProcesarProveedor(string ruta, string nombreProveedor)
        {
            var items = excelService.LeerCatalogo(ruta);
            foreach (var item in items)
            {
                string[] palabrasNuevas = normalizer.ExtraerPalabrasClave(item.Nombre);
                string tamaño = normalizer.ExtraerTamaño(item.Nombre);
                string nombreLimpio = normalizer.LimpiarNombre(item.Nombre);

                var existente = catalogoMaestro.Find(p => normalizer.SonMismoProducto(p.PalabrasClave, palabrasNuevas));

                if (existente != null)
                {
                    existente.PreciosPorProveedor[nombreProveedor] = item.Precio;
                }
                else
                {
                    var nuevoProd = new ProductoComparado
                    {
                        NombreOriginal = item.Nombre,
                        NombreNormalizado = nombreLimpio,
                        Tamaño = tamaño,
                        PalabrasClave = palabrasNuevas
                    };
                    nuevoProd.PreciosPorProveedor[nombreProveedor] = item.Precio;

                    catalogoMaestro.Add(nuevoProd);
                }
            }
        }

        private void CargarGrilla(string filtro = "")
        {
            dgvResultados.SuspendLayout();

            // Limpiamos todo para rehacer las columnas con los nombres nuevos
            dgvResultados.Columns.Clear();
            dgvResultados.Rows.Clear();

            // Columnas dinámicas usando los nombres guardados
            dgvResultados.Columns.Add("Producto", "Producto");
            dgvResultados.Columns.Add("Prov1", nombreProvA);
            dgvResultados.Columns.Add("Prov2", nombreProvB);
            dgvResultados.Columns.Add("Prov3", nombreProvC);
            dgvResultados.Columns.Add("Ganador", "Más Barato");

            dgvResultados.Columns["Producto"].Width = 320;
            for (int i = 1; i < dgvResultados.Columns.Count; i++)
            {
                dgvResultados.Columns[i].Width = 110; // Un poco más anchas para que entre el nombre y el precio bien
            }

            foreach (var prod in catalogoMaestro)
            {
                if (!string.IsNullOrWhiteSpace(filtro) && !prod.NombreOriginal.ToLower().Contains(filtro.ToLower())) continue;

                if (prod.PreciosPorProveedor.Count > 1)
                {
                    // Agregamos el signo $ y forzamos formato numérico
                    string pA = prod.PreciosPorProveedor.ContainsKey(nombreProvA) ? "$ " + prod.PreciosPorProveedor[nombreProvA].ToString("N2") : "-";
                    string pB = prod.PreciosPorProveedor.ContainsKey(nombreProvB) ? "$ " + prod.PreciosPorProveedor[nombreProvB].ToString("N2") : "-";
                    string pC = prod.PreciosPorProveedor.ContainsKey(nombreProvC) ? "$ " + prod.PreciosPorProveedor[nombreProvC].ToString("N2") : "-";

                    dgvResultados.Rows.Add(prod.NombreOriginal, pA, pB, pC, prod.ObtenerProveedorMasBarato());
                }
            }

            dgvResultados.ResumeLayout();
        }

        private void DgvResultados_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Ahora los precios están en las columnas 1, 2 y 3 (ya no existen los bultos)
            if (e.RowIndex >= 0 && (e.ColumnIndex == 1 || e.ColumnIndex == 2 || e.ColumnIndex == 3))
            {
                var row = dgvResultados.Rows[e.RowIndex];

                if (e.Value != null && e.Value.ToString() != "-")
                {
                    string nombreProducto = row.Cells[0].Value.ToString();
                    var producto = catalogoMaestro.Find(p => p.NombreOriginal == nombreProducto);

                    if (producto != null)
                    {
                        // Tomamos el nombre del proveedor del título de la columna (que ahora es dinámico)
                        string proveedorCelda = dgvResultados.Columns[e.ColumnIndex].HeaderText;

                        if (producto.PreciosPorProveedor.ContainsKey(proveedorCelda))
                        {
                            decimal precioDeEsteProveedor = producto.PreciosPorProveedor[proveedorCelda];

                            if (precioDeEsteProveedor == producto.ObtenerPrecioMinimo())
                            {
                                e.CellStyle.BackColor = Color.LightGreen;
                                e.CellStyle.ForeColor = Color.Black;
                                e.CellStyle.Font = new Font(dgvResultados.Font, FontStyle.Bold);
                            }
                        }
                    }
                }
            }
        }
    }
}