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

            // Instanciar el buscador
            txtBuscar = new TextBox() 
            { 
                Location = new Point(20, 55), 
                Width = 380,
                PlaceholderText = "Escribí para buscar un producto..."
            };
            txtBuscar.TextChanged += (s, e) => CargarGrilla(txtBuscar.Text);

            // Eventos de los botones
            btnCargarA.Click += (s, e) => { rutaProvA = SeleccionarArchivo("Proveedor A"); ActualizarEstado(); };
            btnCargarB.Click += (s, e) => { rutaProvB = SeleccionarArchivo("Proveedor B"); ActualizarEstado(); };
            btnCargarC.Click += (s, e) => { rutaProvC = SeleccionarArchivo("Proveedor C"); ActualizarEstado(); };
            btnComparar.Click += BtnComparar_Click;

            // Dibujar la tabla
            dgvResultados = new DataGridView()
            {
                Location = new Point(20, 85),
                Size = new Size(790, 440),
        
                // ACÁ ESTÁ LA MAGIA: Le decimos que se ancle a los 4 lados de la ventana
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

            btnComparar.Text = "Procesando...";
            btnComparar.Enabled = false;
            catalogoMaestro.Clear();

            try
            {
                await Task.Run(() =>
                {
                    if (!string.IsNullOrEmpty(rutaProvA)) ProcesarProveedor(rutaProvA, "Prov A");
                    if (!string.IsNullOrEmpty(rutaProvB)) ProcesarProveedor(rutaProvB, "Prov B");
                    if (!string.IsNullOrEmpty(rutaProvC)) ProcesarProveedor(rutaProvC, "Prov C");
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
            }
        }

        private void ProcesarProveedor(string ruta, string nombreProveedor)
        {
            var items = excelService.LeerCatalogo(ruta);
            foreach (var item in items)
            {
                // Extraemos las palabras UNA SOLA VEZ
                string[] palabrasNuevas = normalizer.ExtraerPalabrasClave(item.Nombre);
                string tamaño = normalizer.ExtraerTamaño(item.Nombre);
                string nombreLimpio = normalizer.LimpiarNombre(item.Nombre);

                // Comparamos usando la memoria rápida
                var existente = catalogoMaestro.Find(p => normalizer.SonMismoProducto(p.PalabrasClave, palabrasNuevas));

                if (existente != null)
                {
                    existente.PreciosPorProveedor[nombreProveedor] = item.Precio;
                    if (item.PrecioBulto > 0) existente.PreciosBultoPorProveedor[nombreProveedor] = item.PrecioBulto;
                }
                else
                {
                    var nuevoProd = new ProductoComparado
                    {
                        NombreOriginal = item.Nombre,
                        NombreNormalizado = nombreLimpio,
                        Tamaño = tamaño,
                        PalabrasClave = palabrasNuevas // Lo guardamos para el futuro
                    };
                    nuevoProd.PreciosPorProveedor[nombreProveedor] = item.Precio;
                    if (item.PrecioBulto > 0) nuevoProd.PreciosBultoPorProveedor[nombreProveedor] = item.PrecioBulto;

                    catalogoMaestro.Add(nuevoProd);
                }
            }
        }

        private void CargarGrilla(string filtro = "")
        {
            // PAUSAMOS EL DIBUJO DE LA PANTALLA (Acelera x10 la carga)
            dgvResultados.SuspendLayout();

            dgvResultados.Rows.Clear();

            if (dgvResultados.Columns.Count == 0)
            {
                dgvResultados.Columns.Add("Producto", "Producto");
                dgvResultados.Columns.Add("ProvA", "Prov A (Uni)");
                dgvResultados.Columns.Add("BultoA", "Prov A (Bulto)");
                dgvResultados.Columns.Add("ProvB", "Prov B (Uni)");
                dgvResultados.Columns.Add("BultoB", "Prov B (Bulto)");
                dgvResultados.Columns.Add("ProvC", "Prov C (Uni)");
                dgvResultados.Columns.Add("BultoC", "Prov C (Bulto)");
                dgvResultados.Columns.Add("Ganador", "Más Barato (Uni)");

                dgvResultados.Columns["Producto"].Width = 320;
                for (int i = 1; i < dgvResultados.Columns.Count; i++)
                {
                    dgvResultados.Columns[i].Width = 90;
                }
            }

            foreach (var prod in catalogoMaestro)
            {
                if (!string.IsNullOrWhiteSpace(filtro) && !prod.NombreOriginal.ToLower().Contains(filtro.ToLower())) continue;

                if (prod.PreciosPorProveedor.Count > 1)
                {
                    string pA = prod.PreciosPorProveedor.ContainsKey("Prov A") ? prod.PreciosPorProveedor["Prov A"].ToString("C2") : "-";
                    string pB = prod.PreciosPorProveedor.ContainsKey("Prov B") ? prod.PreciosPorProveedor["Prov B"].ToString("C2") : "-";
                    string pC = prod.PreciosPorProveedor.ContainsKey("Prov C") ? prod.PreciosPorProveedor["Prov C"].ToString("C2") : "-";

                    string bA = prod.PreciosBultoPorProveedor.ContainsKey("Prov A") ? prod.PreciosBultoPorProveedor["Prov A"].ToString("C2") : "-";
                    string bB = prod.PreciosBultoPorProveedor.ContainsKey("Prov B") ? prod.PreciosBultoPorProveedor["Prov B"].ToString("C2") : "-";
                    string bC = prod.PreciosBultoPorProveedor.ContainsKey("Prov C") ? prod.PreciosBultoPorProveedor["Prov C"].ToString("C2") : "-";

                    dgvResultados.Rows.Add(prod.NombreOriginal, pA, bA, pB, bB, pC, bC, prod.ObtenerProveedorMasBarato());
                }
            }

            // RETOMAMOS EL DIBUJO DE LA PANTALLA
            dgvResultados.ResumeLayout();
        }

        private void DgvResultados_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            // Solo nos interesan las columnas de precio unitario: 1 (Prov A), 3 (Prov B) y 5 (Prov C)
            if (e.RowIndex >= 0 && (e.ColumnIndex == 1 || e.ColumnIndex == 3 || e.ColumnIndex == 5))
            {
                var row = dgvResultados.Rows[e.RowIndex];

                // Si la celda no está vacía y no tiene un guion
                if (e.Value != null && e.Value.ToString() != "-")
                {
                    // 1. Buscamos el producto en nuestro catálogo en memoria
                    string nombreProducto = row.Cells[0].Value.ToString();
                    var producto = catalogoMaestro.Find(p => p.NombreOriginal == nombreProducto);

                    if (producto != null)
                    {
                        // 2. Identificamos qué proveedor estamos mirando según la columna
                        string proveedorCelda = "";
                        if (e.ColumnIndex == 1) proveedorCelda = "Prov A";
                        else if (e.ColumnIndex == 3) proveedorCelda = "Prov B";
                        else if (e.ColumnIndex == 5) proveedorCelda = "Prov C";

                        // 3. Comparamos el precio exacto en memoria, sin importar cómo se vea en pantalla
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