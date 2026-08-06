using ClosedXML.Excel;
using System.Collections.Generic;

namespace ComparadorPrecios.Services
{
    public class ExcelService
    {
        public List<(string Nombre, decimal Precio)> LeerCatalogo(string rutaArchivo)
        {
            var productos = new List<(string Nombre, decimal Precio)>();

            // Abrimos el Excel en modo lectura
            using (var workbook = new XLWorkbook(rutaArchivo))
            {
                // Agarramos la primera hoja (pestaña) del Excel
                var worksheet = workbook.Worksheet(1);
                var filas = worksheet.RangeUsed().RowsUsed();

                foreach (var fila in filas)
                {
                    string nombre = fila.Cell(1).GetString();
                    string precioString = fila.Cell(2).GetString();

                    // TryParse intenta convertir el texto a número. 
                    // Si la fila tiene un encabezado que dice "Precio", esto falla y la saltea automáticamente, evitando errores.
                    if (decimal.TryParse(precioString, out decimal precio))
                    {
                        if (!string.IsNullOrWhiteSpace(nombre))
                        {
                            productos.Add((nombre, precio));
                        }
                    }
                }
            }
            return productos;
        }
    }
}