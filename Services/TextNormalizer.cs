using System.Text.RegularExpressions;
using FuzzySharp;
using ComparadorPrecios.Models;

namespace ComparadorPrecios.Services
{
    public class TextNormalizer
    {
        // Regex para buscar números seguidos de unidades comunes (ej: 500g, 1kg, 2.5lt)
        private static readonly Regex SizeRegex = new Regex(@"(?i)\b\d+(?:[,.]\d+)?\s*(?:kg|g|gr|l|lt|ml)\b");

        public string ExtraerTamaño(string texto)
        {
            var match = SizeRegex.Match(texto);
            return match.Success ? match.Value.ToLower().Replace(" ", "") : "N/A";
        }

        public string LimpiarNombre(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            
            string limpio = texto.ToLower().Trim();
            // Remover el tamaño del nombre para que no ensucie la comparación
            string tamaño = ExtraerTamaño(limpio);
            if (tamaño != "N/A")
            {
                limpio = SizeRegex.Replace(limpio, "").Trim();
            }
            return limpio;
        }

        public bool SonMismoProducto(ProductoComparado p1, string nombreP2, string tamañoP2)
        {
            // Regla 1: Si los tamaños son distintos, NO es el mismo producto.
            if (p1.Tamaño != tamañoP2) return false;

            // Regla 2: Fuzzy Matching. Comparamos los nombres limpios. (Umbral de 85%)
            string nombreLimpioP2 = LimpiarNombre(nombreP2);
            int similitud = Fuzz.TokenSetRatio(p1.NombreNormalizado, nombreLimpioP2);
            
            return similitud >= 85;
        }
    }
}