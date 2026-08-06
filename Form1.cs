using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ComparadorPrecios.Models;
using ComparadorPrecios.Services;

namespace ComparadorPrecios
{
    public partial class Form1 : Form
    {
        // Controles visuales
        private Button btnCargarA;
        private Button btnCargarB;
        private Button btnCargarC;
        private Button btnComparar;
        private DataGridView dgvResultados;
        private Label lblEstado;

        // Servicios y datos en memoria
        private string rutaProvA, rutaProvB, rutaProvC;
        private ExcelService excelService = new ExcelService();
        private TextNormalizer normalizer = new TextNormalizer();
        private List<ProductoComparado> catalogoMaestro = new List<ProductoComparado>();

        public Form1()
        {
            ConfigurarInterfazGrafica();
        }

        private void ConfigurarInterfazGrafica()
        {
            this.Text = "Comparador de Precios Inteligente";
            this.Size = new Size(850, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Dibujar botones
            btnCargarA = new Button() { Text = "Excel Prov A", Location = new Point(20, 20), Width = 120 };
            btnCargarB = new Button() { Text = "Excel Prov B", Location = new Point(150, 20), Width = 120 };
            btnCargarC = new Button() { Text = "Excel Prov C", Location = new Point(280, 20), Width = 120 };
            btnComparar = new Button() { Text = "COMPARAR", Location = new Point(420, 20), Width = 120, BackColor = Color.LightBlue, Font = new Font("Arial", 9, FontStyle.Bold) };
            
            lblEstado = new Label() { Text = "Esperando archivos...", Location = new Point(560, 25), Width = 250, ForeColor = Color.Gray };

            // Eventos de los botones
            btnCargarA.Click += (s, e) => { rutaProvA = SeleccionarArchivo("Proveedor A"); ActualizarEstado(); };
            btnCargarB.Click += (s, e) => { rutaProvB = SeleccionarArchivo("Proveedor B"); ActualizarEstado(); };
            btnCargarC.Click += (s, e) => { rutaProvC = SeleccionarArchivo("Proveedor C"); ActualizarEstado(); };
            btnComparar.Click += BtnComparar_Click;

            // Dibujar la tabla
            dgvResultados = new DataGridView()
            {
                Location = new Point(20, 70),
                Size = new Size(790, 450),
                AllowUserToAddRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.WhiteSmoke
            };
            dgvResultados.CellFormatting += DgvResultados_CellFormatting;

            // Agregar todo a la ventana
            this.Controls.Add(btnCargarA);
            this.Controls.Add(btnCargarB);
            this.Controls.Add(btnCargarC);
            this.Controls.Add(btnComparar);
            this.Controls.Add(lblEstado);
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

        private void BtnComparar_Click(object sender, EventArgs e)
        {
            if (rutaProvA == null && rutaProvB == null && rutaProvC == null)
            {
                MessageBox.Show("Por favor, cargá al menos un archivo Excel.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnComparar.Text = "Procesando...";
            btnComparar.Enabled = false;
            catalogoMaestro.Clear();

            try
            {
                if (!string.IsNullOrEmpty(rutaProvA)) ProcesarProveedor(rutaProvA, "Prov A");
                if (!string.IsNullOrEmpty(rutaProvB)) ProcesarProveedor(rutaProvB, "Prov B");
                if (!string.IsNullOrEmpty(rutaProvC)) ProcesarProveedor(rutaProvC, "Prov C");

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
            }
        }

        private void ProcesarProveedor(string ruta, string nombreProveedor)
        {
            var items = excelService.LeerCatalogo(ruta);
            foreach (var item in items)
            {
                string tamaño = normalizer.ExtraerTamaño(item.Nombre);
                string nombreLimpio = normalizer.LimpiarNombre(item.Nombre);
                var existente = catalogoMaestro.Find(p => normalizer.SonMismoProducto(p, item.Nombre, tamaño));

                if (existente != null)
                {
                    existente.PreciosPorProveedor[nombreProveedor] = item.Precio;
                }
                else
                {
                    var nuevoProd = new ProductoComparado { NombreOriginal = item.Nombre, NombreNormalizado = nombreLimpio, Tamaño = tamaño };
                    nuevoProd.PreciosPorProveedor[nombreProveedor] = item.Precio;
                    catalogoMaestro.Add(nuevoProd);
                }
            }
        }

        private void CargarGrilla()
        {
            dgvResultados.Rows.Clear();
            dgvResultados.Columns.Clear();

            dgvResultados.Columns.Add("Producto", "Producto");
            dgvResultados.Columns.Add("Tamaño", "Tamaño");
            dgvResultados.Columns.Add("ProvA", "Prov A");
            dgvResultados.Columns.Add("ProvB", "Prov B");
            dgvResultados.Columns.Add("ProvC", "Prov C");
            dgvResultados.Columns.Add("Ganador", "Más Barato");

            foreach (var prod in catalogoMaestro)
            {
                // Mostrar productos que tengan competencia (mínimo 2 proveedores)
                if (prod.PreciosPorProveedor.Count > 1) 
                {
                    string pA = prod.PreciosPorProveedor.ContainsKey("Prov A") ? prod.PreciosPorProveedor["Prov A"].ToString("C2") : "-";
                    string pB = prod.PreciosPorProveedor.ContainsKey("Prov B") ? prod.PreciosPorProveedor["Prov B"].ToString("C2") : "-";
                    string pC = prod.PreciosPorProveedor.ContainsKey("Prov C") ? prod.PreciosPorProveedor["Prov C"].ToString("C2") : "-";

                    dgvResultados.Rows.Add(prod.NombreOriginal, prod.Tamaño, pA, pB, pC, prod.ObtenerProveedorMasBarato());
                }
            }
        }

        private void DgvResultados_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Pintar de verde el precio ganador
            if (e.RowIndex >= 0 && e.ColumnIndex >= 2 && e.ColumnIndex <= 4)
            {
                var row = dgvResultados.Rows[e.RowIndex];
                if (e.Value != null && e.Value.ToString() != "-")
                {
                    // Limpiar el formato de moneda ($) para poder comparar el número
                    string valorLimpio = e.Value.ToString().Replace("$", "").Trim();
                    if (decimal.TryParse(valorLimpio, out decimal precioCelda))
                    {
                        var producto = catalogoMaestro.Find(p => p.NombreOriginal == row.Cells[0].Value.ToString());
                        if (producto != null && precioCelda == producto.ObtenerPrecioMinimo())
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