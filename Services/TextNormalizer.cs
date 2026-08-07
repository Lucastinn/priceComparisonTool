using System;
using System.Collections.Generic;

namespace ComparadorPrecios.Services
{
    public class TextNormalizer
    {
        // 1. EL DICCIONARIO: Acá le enseñamos al programa las abreviaciones
        private readonly Dictionary<string, string> sinonimos = new Dictionary<string, string>
        {
            { "ac", "acondicionador" },
            { "sh", "shampoo" },
            { "desod", "desodorante" },
            { "yer", "yerba" }
            // ¡Podés ir agregando más palabras acá a medida que encuentres otras abreviaciones!
        };

        public string[] ExtraerPalabrasClave(string nombre)
        {
            // 2. Limpiamos puntos, guiones y ahora también COMAS
            string textoLimpio = nombre.ToLower()
                                       .Replace("-", " ")
                                       .Replace(".", "")
                                       .Replace(",", "");

            var palabrasOriginales = textoLimpio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var palabrasFinales = new List<string>();

            foreach (var palabra in palabrasOriginales)
            {
                string p = palabra;

                // 3. Traducimos la palabra si está en nuestro diccionario
                if (sinonimos.ContainsKey(p))
                {
                    p = sinonimos[p];
                }

                // 4. Domar la letra "X" de los tamaños
                if (p == "x") continue; // Si la "x" está sola, la ignoramos por completo

                // Si la palabra arranca con "x" y le sigue un número (ej: "x15l"), le cortamos la x
                if (p.StartsWith("x") && p.Length > 1 && char.IsDigit(p[1]))
                {
                    p = p.Substring(1);
                }

                palabrasFinales.Add(p);
            }

            return palabrasFinales.ToArray();
        }

        public bool SonMismoProducto(string[] palabras1, string[] palabras2)
        {
            if (palabras1 == null || palabras2 == null || palabras1.Length == 0 || palabras2.Length == 0) return false;

            int coincidencias = 0;
            foreach (var p1 in palabras1)
            {
                foreach (var p2 in palabras2)
                {
                    // La regla de las 3 letras sigue viva para no confundir conectores, 
                    // pero como ahora "ac" se transformó en "acondicionador", pasa perfecto.
                    if (p1 == p2 || (p1.Length >= 3 && p2.StartsWith(p1)) || (p2.Length >= 3 && p1.StartsWith(p2)))
                    {
                        coincidencias++;
                        break;
                    }
                }
            }

            int palabrasMinimas = Math.Min(palabras1.Length, palabras2.Length);

            // Bajamos la exigencia al 65% porque a veces un proveedor agrega palabras extra como "girasol" o "clasico"
            if (palabrasMinimas > 0 && ((double)coincidencias / palabrasMinimas) >= 0.65)
            {
                return true;
            }

            return false;
        }

        public string ExtraerTamaño(string nombre)
        {
            // Mantenemos este método por si lo usás en otro lado, aunque ahora la comparación no depende de esto
            return "";
        }

        public string LimpiarNombre(string nombre)
        {
            return nombre;
        }
    }
}