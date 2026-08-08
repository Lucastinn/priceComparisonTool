using ClosedXML.Excel;
using System;
using System.Windows.Forms;

namespace ComparadorPrecios.Services
{
    public class ExportService
    {
        public void ExportarAExcel(DataGridView dgv, string rutaArchivo, bool soloSeleccionados)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Mejores Precios");

                // 1. Títulos de las columnas del Excel
                worksheet.Cell(1, 1).Value = "Producto";
                worksheet.Cell(1, 2).Value = "Mejor Proveedor";
                worksheet.Cell(1, 3).Value = "Precio";

                var rangoTitulos = worksheet.Range("A1:C1");
                rangoTitulos.Style.Font.Bold = true;
                rangoTitulos.Style.Fill.BackgroundColor = XLColor.LightBlue;

                int rowExcel = 2;

                // 2. Recorrer la tabla de la pantalla
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;

                    // Revisamos si la casilla está marcada
                    bool seleccionado = row.Cells["Seleccionar"].Value != null && (bool)row.Cells["Seleccionar"].Value;

                    if (soloSeleccionados && !seleccionado) continue; // Si solo exportamos seleccionados y este no lo está, lo salteamos

                    string producto = row.Cells["Producto"].Value?.ToString() ?? "";
                    string ganadorStr = row.Cells["Ganador"].Value?.ToString() ?? "";

                    if (ganadorStr != "N/A" && ganadorStr != "-")
                    {
                        string proveedor = ganadorStr;
                        string precioStr = "";

                        // Separamos el string "Prov A ($ 1500.00)" en dos variables
                        int idx = ganadorStr.IndexOf("($");
                        if (idx >= 0)
                        {
                            proveedor = ganadorStr.Substring(0, idx).Trim();
                            precioStr = ganadorStr.Substring(idx + 2).Replace(")", "").Trim();
                        }

                        // Escribimos en el Excel
                        worksheet.Cell(rowExcel, 1).Value = producto;
                        worksheet.Cell(rowExcel, 2).Value = proveedor;

                        if (decimal.TryParse(precioStr, out decimal p))
                        {
                            worksheet.Cell(rowExcel, 3).Value = p;
                            worksheet.Cell(rowExcel, 3).Style.NumberFormat.Format = "$ #,##0.00"; // Formato moneda
                        }
                        else
                        {
                            worksheet.Cell(rowExcel, 3).Value = precioStr;
                        }

                        rowExcel++;
                    }
                }

                // Auto-ajustamos el ancho de las columnas
                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(rutaArchivo);
            }
        }
    }
}