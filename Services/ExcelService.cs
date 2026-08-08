using ClosedXML.Excel;
using System.Collections.Generic;
using System.Globalization;

namespace ComparadorPrecios.Services
{
    public class ExcelService
    {
        public List<(string Nombre, decimal Precio)> LeerCatalogo(string rutaArchivo)
        {
            var productos = new List<(string Nombre, decimal Precio)>();

            using (var workbook = new XLWorkbook(rutaArchivo))
            {
                var worksheet = workbook.Worksheet(1);
                var filas = worksheet.RowsUsed();

                int colNombre = -1;
                int colPrecio = -1;
                bool encabezadosEncontrados = false;

                foreach (var fila in filas)
                {
                    // 1. BUSCAMOS LOS ENCABEZADOS EN UNA MISMA FILA
                    if (!encabezadosEncontrados)
                    {
                        int tempColNombre = -1;
                        int tempColPrecio = -1;

                        foreach (var celda in fila.CellsUsed())
                        {
                            string valor = celda.GetString().ToLower().Trim();

                            if (valor.Contains("producto") || valor.Contains("descripcion") || valor.Contains("descripción") || valor.Contains("nombre") || valor.Contains("articulo") || valor.Contains("detalle"))
                                tempColNombre = celda.Address.ColumnNumber;

                            else if (valor.Contains("lista") || valor.Contains("precio") || valor.Contains("pvp") || valor.Contains("valor") || valor.Contains("importe") || valor.Contains("costo") || valor.Contains("neto") || valor.Contains("final"))
                                tempColPrecio = celda.Address.ColumnNumber;
                        }

                        // Solo damos por válido si EN LA MISMA FILA encontró los dos títulos
                        if (tempColNombre != -1 && tempColPrecio != -1)
                        {
                            colNombre = tempColNombre;
                            colPrecio = tempColPrecio;
                            encabezadosEncontrados = true;
                        }

                        continue; // Pasamos a la siguiente fila (no leemos datos del título)
                    }

                    // 2. EXTRAEMOS LOS DATOS
                    string nombre = fila.Cell(colNombre).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(nombre)) continue;

                    decimal precio = 0m;
                    var celPrecio = fila.Cell(colPrecio);

                    if (celPrecio.DataType == XLDataType.Number)
                    {
                        try { precio = (decimal)celPrecio.GetDouble(); }
                        catch { precio = 0m; }
                    }
                    else
                    {
                        string precioString = celPrecio.GetString().Replace("$", "").Replace(" ", "").Trim();
                        if (!decimal.TryParse(precioString, NumberStyles.Any, CultureInfo.CurrentCulture, out precio)
                            && !decimal.TryParse(precioString, NumberStyles.Any, new CultureInfo("es-AR"), out precio)
                            && !decimal.TryParse(precioString, NumberStyles.Any, CultureInfo.InvariantCulture, out precio))
                        {
                            continue;
                        }
                    }

                    if (precio > 0)
                    {
                        productos.Add((nombre, precio));
                    }
                }
            }

            return productos;
        }
    }
}