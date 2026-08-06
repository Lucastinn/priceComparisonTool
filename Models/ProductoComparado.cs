using System.Collections.Generic;
using System.Linq;

namespace ComparadorPrecios.Models
{
    public class ProductoComparado
    {
        public string NombreOriginal { get; set; }
        public string NombreNormalizado { get; set; }
        public string Tamaño { get; set; }
        
        // Usamos un diccionario para manejar de qué proveedor viene qué precio
        public Dictionary<string, decimal> PreciosPorProveedor { get; set; } = new Dictionary<string, decimal>();

        // Lógica encapsulada para obtener el mejor precio
        public string ObtenerProveedorMasBarato()
        {
            if (PreciosPorProveedor.Count == 0) return "N/A";
            var minimo = PreciosPorProveedor.OrderBy(x => x.Value).First();
            return $"{minimo.Key} (${minimo.Value})";
        }

        public decimal ObtenerPrecioMinimo()
        {
            if (PreciosPorProveedor.Count == 0) return 0;
            return PreciosPorProveedor.Values.Min();
        }
    }
}