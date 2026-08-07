using System.Collections.Generic;
using System.Linq;

namespace ComparadorPrecios.Models
{
    public class ProductoComparado
    {
        public string NombreOriginal { get; set; }
        public string NombreNormalizado { get; set; }
        public string Tamaño { get; set; }
        public string[] PalabrasClave { get; set; }

        public Dictionary<string, decimal> PreciosPorProveedor { get; set; } = new Dictionary<string, decimal>();

        public string ObtenerProveedorMasBarato()
        {
            if (PreciosPorProveedor.Count == 0) return "N/A";
            var minimo = PreciosPorProveedor.OrderBy(x => x.Value).First();
            // Acá ya va a devolver el nombre personalizado que escriba el cliente
            return $"{minimo.Key} ($ {minimo.Value.ToString("N2")})";
        }

        public decimal ObtenerPrecioMinimo()
        {
            if (PreciosPorProveedor.Count == 0) return 0;
            return PreciosPorProveedor.Values.Min();
        }
    }
}