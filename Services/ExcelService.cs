using ClosedXML.Excel;
using System.Collections.Generic;

namespace ComparadorPrecios.Services
{
    public class ExcelService
    {
        // Modificamos para que devuelva 3 datos: Nombre, Precio y PrecioBulto
        public List<(string Nombre, decimal Precio, decimal PrecioBulto)> LeerCatalogo(string rutaArchivo)
        {
            var productos = new List<(string Nombre, decimal Precio, decimal PrecioBulto)>();

            using (var workbook = new XLWorkbook(rutaArchivo))
            {
                var worksheet = workbook.Worksheet(1);
                var filas = worksheet.RangeUsed().RowsUsed();

                int colNombre = -1;
                int colPrecio = -1;
                int colBulto = -1; // Nueva variable para ubicar la columna de bultos
                bool encabezadosEncontrados = false;

                foreach (var fila in filas)
                {
                    if (!encabezadosEncontrados)
                    {
                        foreach (var celda in fila.CellsUsed())
                        {
                            string valor = celda.GetString().ToLower();
                            
                            if (valor.Contains("producto") || valor.Contains("descripcion") || valor.Contains("descripción"))
                            {
                                colNombre = celda.Address.ColumnNumber;
                            }
                            else if (valor.Contains("lista") || valor.Contains("precio"))
                            {
                                if (colPrecio == -1) colPrecio = celda.Address.ColumnNumber; 
                            }
                            // Detectamos la columna de unidades por bulto
                            else if (valor.Contains("bulto"))
                            {
                                colBulto = celda.Address.ColumnNumber;
                            }
                        }

                        if (colNombre != -1 && colPrecio != -1)
                        {
                            encabezadosEncontrados = true;
                            continue; 
                        }
                        continue; 
                    }

                    string nombre = fila.Cell(colNombre).GetString();
                    string precioString = fila.Cell(colPrecio).GetString();
                    decimal precioBulto = 0;

                    if (decimal.TryParse(precioString, out decimal precio))
                    {
                        if (!string.IsNullOrWhiteSpace(nombre))
                        {
                            // Si detectamos columna de bulto, intentamos extraer la cantidad y multiplicar
                            if (colBulto != -1)
                            {
                                string bultoString = fila.Cell(colBulto).GetString();
                                if (decimal.TryParse(bultoString, out decimal cantBulto) && cantBulto > 0)
                                {
                                    precioBulto = precio * cantBulto;
                                }
                            }

                            productos.Add((nombre, precio, precioBulto));
                        }
                    }
                }
            }
            return productos;
        }
    }
}