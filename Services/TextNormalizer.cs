using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ComparadorPrecios.Services
{
    public class TextNormalizer
    {
        private readonly Dictionary<string, string> sinonimos = new Dictionary<string, string>
        {
            { "ac", "acondicionador" },
            { "sh", "shampoo" },
            { "desod", "desodorante" },
            { "yer", "yerba" },
            { "gr", "g" },
            { "grs", "g" },
            { "kg", "kilo" },
            { "kgs", "kilo" },
            { "cc", "ml" },
            { "cj", "caja" },
            { "un", "unidad" },
            {"bco", "blanco"},
            {"KG", "kilo"},
            {"G", "g"},
            {"GR", "g"},
            {"GRS", "g"},
            {"ML", "ml"},
            {"L", "l"},
            {"LT", "l"},
            {"LTS.", "l"},
            {"LTs", "l"},
            {"LITRO", "l"}
        };

        private readonly HashSet<string> palabrasIgnoradas = new HashSet<string>
        {
            "de", "la", "el", "los", "las", "con", "y", "en", "por", "para", "sabor", "tipo", "del", "al"
        };

        public string[] ExtraerPalabrasClave(string nombre)
        {
            string textoLimpio = nombre.ToLower().Replace("-", " ").Replace(".", " ").Replace(",", " ").Replace("/", " ");

            // Separamos números de letras (ej: "200g" -> "200 g")
            textoLimpio = Regex.Replace(textoLimpio, @"(\d+)([a-z]+)", "$1 $2");
            textoLimpio = Regex.Replace(textoLimpio, @"([a-z]+)(\d+)", "$1 $2");

            var palabrasOriginales = textoLimpio.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var palabrasFinales = new List<string>();

            foreach (var palabra in palabrasOriginales)
            {
                string p = palabra;
                if (palabrasIgnoradas.Contains(p)) continue;
                if (sinonimos.ContainsKey(p)) p = sinonimos[p];
                if (p == "x") continue;
                palabrasFinales.Add(p);
            }

            return palabrasFinales.ToArray();
        }

        public bool SonMismoProducto(string[] palabras1, string[] palabras2)
        {
            if (palabras1 == null || palabras2 == null || palabras1.Length == 0 || palabras2.Length == 0) return false;

            // --- 1. REGLA ESTRICTA DE NÚMEROS (PESOS/TAMAÑOS) ---
            var numeros1 = palabras1.Where(p => p.All(char.IsDigit)).ToList();
            var numeros2 = palabras2.Where(p => p.All(char.IsDigit)).ToList();

            if (numeros1.Count > 0 && numeros2.Count > 0)
            {
                // Buscamos si hay números que se contradicen (ej: uno tiene 200 y el otro 350)
                var distintosEn1 = numeros1.Except(numeros2).ToList();
                var distintosEn2 = numeros2.Except(numeros1).ToList();

                // Si AMBOS textos tienen números exclusivos que el otro no tiene, es un choque directo
                if (distintosEn1.Count > 0 && distintosEn2.Count > 0)
                {
                    return false; // Cortamos el match acá mismo. Son tamaños distintos.
                }
            }

            // --- 2. REGLA DE PALABRAS (SABORES Y TIPOS) ---
            int coincidencias = 0;
            bool[] visitados = new bool[palabras2.Length];

            foreach (var p1 in palabras1)
            {
                for (int j = 0; j < palabras2.Length; j++)
                {
                    if (visitados[j]) continue;

                    string p2 = palabras2[j];

                    if (p1 == p2 || (p1.Length >= 4 && p2.StartsWith(p1)) || (p2.Length >= 4 && p1.StartsWith(p2)))
                    {
                        coincidencias++;
                        visitados[j] = true;
                        break;
                    }
                }
            }

            double promedioPalabras = (palabras1.Length + palabras2.Length) / 2.0;

            // Subimos la exigencia al 82% para evitar que "pepas" se junte con "surtidas" o "agua" con "aceite"
            if (promedioPalabras > 0 && (coincidencias / promedioPalabras) >= 0.82)
            {
                return true;
            }

            return false;
        }

        public string ExtraerTamaño(string nombre) { return ""; }
        public string LimpiarNombre(string nombre) { return nombre; }
    }
}