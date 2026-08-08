using System.Collections.Generic;
using System.Linq;
using ComparadorPrecios.Services;

namespace ComparadorPrecios.Models
{
    public class ProductoComparado
    {
        public string NombreOriginal { get; set; }
        public string NombreNormalizado { get; set; }
        public string Tamaño { get; set; }
        public ClavesProducto Claves { get; set; }

        // NUEVO: Memoria para saber si el usuario lo tildó
        public bool SeleccionadoParaExportar { get; set; } = false;

        public Dictionary<string, decimal> PreciosPorProveedor { get; set; } = new Dictionary<string, decimal>();

        public string ObtenerProveedorMasBarato()
        {
            if (PreciosPorProveedor.Count == 0) return "N/A";
            var minimo = PreciosPorProveedor.OrderBy(x => x.Value).First();
            return $"{minimo.Key} ($ {minimo.Value.ToString("N2")})";
        }

        public decimal ObtenerPrecioMinimo()
        {
            if (PreciosPorProveedor.Count == 0) return 0;
            return PreciosPorProveedor.Values.Min();
        }
    }
}