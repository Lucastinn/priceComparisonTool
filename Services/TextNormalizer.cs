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

        // 1. Prepara y limpia las palabras una sola vez
public string[] ExtraerPalabrasClave(string nombre)
{
    return nombre.ToLower().Replace("-", " ").Replace(".", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
}

// 2. Compara directamente los arreglos en memoria (¡Rapidísimo!)
public bool SonMismoProducto(string[] palabras1, string[] palabras2)
{
    if (palabras1 == null || palabras2 == null) return false;

    int coincidencias = 0;
    foreach (var p1 in palabras1)
    {
        foreach (var p2 in palabras2)
        {
            if (p1 == p2 || (p1.Length >= 3 && p2.StartsWith(p1)) || (p2.Length >= 3 && p1.StartsWith(p2)))
            {
                coincidencias++;
                break; 
            }
        }
    }

    int palabrasMinimas = Math.Min(palabras1.Length, palabras2.Length);
    if (palabrasMinimas > 0 && ((double)coincidencias / palabrasMinimas) >= 0.75) 
    {
        return true;
    }

    return false;
}
    }
}