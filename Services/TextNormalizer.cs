using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ComparadorPrecios.Services
{
    /// <summary>
    /// Tipo de magnitud física detectada en el nombre de un producto.
    /// Dos medidas solo se comparan entre sí si son del mismo tipo
    /// (un peso nunca contradice a un volumen).
    /// </summary>
    public enum TipoDimension
    {
        PesoGramos,
        VolumenMililitros,
        Unidades
    }

    /// <summary>
    /// Una medida (peso, volumen o cantidad de unidades) ya normalizada
    /// a una unidad base, para poder comparar "1.5L" contra "1500ML" sin ambigüedad.
    /// </summary>
    public class Medida
    {
        public TipoDimension Tipo { get; set; }
        public double Valor { get; set; }

        public override string ToString()
        {
            string sufijo = Tipo == TipoDimension.PesoGramos ? "g"
                          : Tipo == TipoDimension.VolumenMililitros ? "ml"
                          : "un";
            return $"{Valor:0.###}{sufijo}";
        }
    }

    /// <summary>
    /// Token de texto ponderado. El peso determina cuánto aporta al score
    /// final de similitud entre dos productos.
    /// </summary>
    public class TokenPonderado
    {
        public string Texto;
        public double Peso;

        /// <summary>
        /// True si el token es un número que NO formaba parte de una medida
        /// (ej: el "9" de "9 DE ORO"). Estos números nunca se comparan por fuzzy,
        /// solo por igualdad exacta, y pesan más porque suelen identificar marca/línea.
        /// </summary>
        public bool EsNumeroSuelto;

        public TokenPonderado(string texto, double peso, bool esNumeroSuelto = false)
        {
            Texto = texto;
            Peso = peso;
            EsNumeroSuelto = esNumeroSuelto;
        }
    }

    /// <summary>
    /// Resultado de analizar el nombre de un producto: sus palabras clave ponderadas
    /// + las medidas físicas que se detectaron y se separaron del texto.
    /// Este es el objeto que hay que guardar en el catálogo maestro
    /// (reemplaza al viejo `string[] PalabrasClave`).
    /// </summary>
    public class ClavesProducto
    {
        public string NombreOriginal;
        public List<TokenPonderado> Tokens = new List<TokenPonderado>();
        public List<Medida> Medidas = new List<Medida>();
    }

    public class TextNormalizer
    {
        // ============================================================
        // UMBRALES (ajustables según los resultados con tus 6000 items)
        // ============================================================

        /// Similitud mínima 0-1 para que dos palabras se consideren "la misma" vía Levenshtein.
        private const double UMBRAL_SIMILITUD_FUZZY = 0.78;

        /// Score final mínimo 0-1 para considerar que dos productos son el mismo.
        private const double UMBRAL_SCORE = 0.66;

        /// Tolerancia relativa entre medidas para considerarlas "iguales" (redondeos de conversión).
        private const double TOLERANCIA_MEDIDA = 0.01; // 1%

        private const double PESO_NUMERO_MARCA = 3.0;
        private const double PESO_PALABRA_NORMAL = 1.0;
        private const double PESO_PALABRA_DESCRIPTIVA = 0.4;
        private const double PESO_PALABRA_CORTA = 0.5;

        // ============================================================
        // DICCIONARIOS (extendé estos según tus proveedores reales)
        // ============================================================

        private readonly Dictionary<string, string> sinonimos = new Dictionary<string, string>
        {
            { "ac", "acondicionador" },
            { "sh", "shampoo" },
            { "desod", "desodorante" },
            { "yer", "yerba" },
            { "bco", "blanco" },
        };

        private readonly HashSet<string> palabrasIgnoradas = new HashSet<string>
        {
            "de", "la", "el", "los", "las", "con", "y", "en", "por", "para",
            "sabor", "tipo", "del", "al", "c", "s"
        };

        /// <summary>
        /// Palabras "de relleno" que algunos proveedores agregan y otros no.
        /// No se ignoran del todo (a veces SÍ distinguen una variante real),
        /// pero pesan poco para que no arruinen el match del núcleo del producto.
        /// </summary>
        private readonly HashSet<string> palabrasDescriptivasComunes = new HashSet<string>
        {
            "premium", "clasico", "clásico", "tradicional", "original", "especial",
            "chocolate", "vainilla", "girasol", "maiz", "maíz", "oliva",
            "light", "diet", "natural", "surtido", "surtidos"
        };

        /// Unidades reconocidas -> (dimensión física, factor de conversión a la unidad base).
        private readonly Dictionary<string, (TipoDimension tipo, double factor)> unidades =
            new Dictionary<string, (TipoDimension, double)>
        {
            { "g", (TipoDimension.PesoGramos, 1) },
            { "gr", (TipoDimension.PesoGramos, 1) },
            { "grs", (TipoDimension.PesoGramos, 1) },
            { "gramos", (TipoDimension.PesoGramos, 1) },
            { "mg", (TipoDimension.PesoGramos, 0.001) },
            { "kg", (TipoDimension.PesoGramos, 1000) },
            { "kgs", (TipoDimension.PesoGramos, 1000) },
            { "kilo", (TipoDimension.PesoGramos, 1000) },
            { "kilos", (TipoDimension.PesoGramos, 1000) },
            { "ml", (TipoDimension.VolumenMililitros, 1) },
            { "cc", (TipoDimension.VolumenMililitros, 1) },
            { "l", (TipoDimension.VolumenMililitros, 1000) },
            { "lt", (TipoDimension.VolumenMililitros, 1000) },
            { "lts", (TipoDimension.VolumenMililitros, 1000) },
            { "litro", (TipoDimension.VolumenMililitros, 1000) },
            { "litros", (TipoDimension.VolumenMililitros, 1000) },
            { "un", (TipoDimension.Unidades, 1) },
            { "unid", (TipoDimension.Unidades, 1) },
            { "unids", (TipoDimension.Unidades, 1) },
            { "unidad", (TipoDimension.Unidades, 1) },
            { "unidades", (TipoDimension.Unidades, 1) },
        };

        // ============================================================
        // API PRINCIPAL
        // ============================================================

        /// <summary>
        /// Analiza el nombre crudo de un producto y devuelve sus palabras clave
        /// ponderadas + las medidas detectadas. Guardá el resultado en el catálogo
        /// (reemplaza a ExtraerPalabrasClave del código viejo).
        /// </summary>
        public ClavesProducto AnalizarProducto(string nombre)
        {
            var resultado = new ClavesProducto { NombreOriginal = nombre };
            if (string.IsNullOrWhiteSpace(nombre)) return resultado;

            string texto = PreProcesar(nombre);
            var tokensCrudos = texto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            int i = 0;
            while (i < tokensCrudos.Length)
            {
                string tok = tokensCrudos[i];

                if (EsNumero(tok))
                {
                    if (i + 1 < tokensCrudos.Length && unidades.ContainsKey(tokensCrudos[i + 1]))
                    {
                        var unidad = unidades[tokensCrudos[i + 1]];
                        double valor = double.Parse(tok, CultureInfo.InvariantCulture);
                        resultado.Medidas.Add(new Medida { Tipo = unidad.tipo, Valor = valor * unidad.factor });
                        i += 2; // consumimos número + unidad
                        continue;
                    }

                    // Número suelto: no tiene unidad al lado -> probable identificador de marca/línea (ej: el "9" de "9 de Oro")
                    resultado.Tokens.Add(new TokenPonderado(tok, PESO_NUMERO_MARCA, esNumeroSuelto: true));
                    i++;
                    continue;
                }

                if (tok == "x" || palabrasIgnoradas.Contains(tok))
                {
                    i++;
                    continue;
                }

                string palabra = sinonimos.ContainsKey(tok) ? sinonimos[tok] : tok;

                double peso;
                if (palabra.Length <= 2) peso = PESO_PALABRA_CORTA;
                else if (palabrasDescriptivasComunes.Contains(palabra)) peso = PESO_PALABRA_DESCRIPTIVA;
                else peso = PESO_PALABRA_NORMAL;

                resultado.Tokens.Add(new TokenPonderado(palabra, peso));
                i++;
            }

            return resultado;
        }

        /// <summary>True si dos productos ya analizados deben considerarse el mismo producto.</summary>
        public bool SonMismoProducto(ClavesProducto a, ClavesProducto b) => SonMismoProducto(a, b, out _);

        /// <summary>
        /// Igual que el anterior, pero además devuelve el score final (0-1) por si querés
        /// loguearlo, ajustar UMBRAL_SCORE, o mostrar en la UI los matches "dudosos" para revisión manual.
        /// </summary>
        public bool SonMismoProducto(ClavesProducto a, ClavesProducto b, out double score)
        {
            score = 0;
            if (a == null || b == null || a.Tokens.Count == 0 || b.Tokens.Count == 0) return false;

            // 1) HARD-FAIL: medidas que se contradicen explícitamente
            if (HayContradiccionDeMedidas(a.Medidas, b.Medidas)) return false;

            // 2) Matching ponderado + fuzzy de palabras
            score = CalcularScore(a.Tokens, b.Tokens);
            return score >= UMBRAL_SCORE;
        }

        // ============================================================
        // 1. HARD-FAIL DE MEDIDAS
        // ============================================================

        private bool HayContradiccionDeMedidas(List<Medida> m1, List<Medida> m2)
        {
            foreach (var med1 in m1)
            {
                foreach (var med2 in m2)
                {
                    if (med1.Tipo != med2.Tipo) continue; // un peso no contradice a un volumen

                    double diferenciaRelativa = Math.Abs(med1.Valor - med2.Valor) / Math.Max(med1.Valor, med2.Valor);
                    if (diferenciaRelativa > TOLERANCIA_MEDIDA)
                        return true; // mismo tipo de medida, valores distintos -> productos distintos
                }
            }
            return false;
        }

        // ============================================================
        // 2. SCORING PONDERADO + FUZZY
        // ============================================================

        private double CalcularScore(List<TokenPonderado> tokens1, List<TokenPonderado> tokens2)
        {
            bool[] usados2 = new bool[tokens2.Count];
            double pesoCoincidente = 0;

            foreach (var t1 in tokens1)
            {
                int mejorIndice = -1;
                double mejorSimilitud = 0;

                for (int j = 0; j < tokens2.Count; j++)
                {
                    if (usados2[j]) continue;
                    double similitud = Similitud(t1, tokens2[j]);
                    if (similitud > mejorSimilitud)
                    {
                        mejorSimilitud = similitud;
                        mejorIndice = j;
                    }
                }

                if (mejorIndice >= 0 && mejorSimilitud >= UMBRAL_SIMILITUD_FUZZY)
                {
                    usados2[mejorIndice] = true;
                    pesoCoincidente += (t1.Peso + tokens2[mejorIndice].Peso) / 2.0 * mejorSimilitud;
                }
            }

            double pesoTotal = tokens1.Sum(t => t.Peso) + tokens2.Sum(t => t.Peso);
            if (pesoTotal <= 0) return 0;

            // Coeficiente de Dice ponderado: 2 * coincidencias / (peso total de ambos lados)
            return (2.0 * pesoCoincidente) / pesoTotal;
        }

        private double Similitud(TokenPonderado t1, TokenPonderado t2)
        {
            // Los números (marca/línea) exigen coincidencia EXACTA: no tiene sentido
            // "fuzzy-matchear" un 7 con un 9, aunque Levenshtein los vea "parecidos".
            if (t1.EsNumeroSuelto || t2.EsNumeroSuelto)
                return t1.Texto == t2.Texto ? 1.0 : 0.0;

            if (t1.Texto == t2.Texto) return 1.0;

            // Prefijo compartido largo (ej: "gall" vs "galletita", truncamientos) -> match fuerte directo
            if ((t1.Texto.Length >= 4 && t2.Texto.StartsWith(t1.Texto)) ||
                (t2.Texto.Length >= 4 && t1.Texto.StartsWith(t2.Texto)))
                return 0.95;

            // Levenshtein normalizado -> tolera typos, género (o/a), plural (s)
            int distancia = Levenshtein(t1.Texto, t2.Texto);
            int largoMax = Math.Max(t1.Texto.Length, t2.Texto.Length);
            return largoMax == 0 ? 0 : 1.0 - ((double)distancia / largoMax);
        }

        // ============================================================
        // PREPROCESAMIENTO DE TEXTO
        // ============================================================

        private string PreProcesar(string nombre)
        {
            string t = nombre.ToLowerInvariant();

            // cm3 es un caso raro que rompe la separación dígito/letra (tiene un dígito
            // adentro de la unidad) -> lo normalizamos a "cc" antes de todo lo demás.
            t = Regex.Replace(t, @"cm3|cm³", "cc");

            // 1) Unificar decimales: "1,5" -> "1.5" (solo si la coma está ENTRE dígitos,
            //    para no tocar comas usadas como separador de lista).
            t = Regex.Replace(t, @"(?<=\d),(?=\d)", ".");

            // 2) Separar "x" pegado a un número (multiplicador de empaque):
            //    "GALLX120G" -> "GALL X120G". Sin esto, la abreviatura del proveedor
            //    se come el multiplicador y "galletita" nunca hace match con "gallx".
            t = Regex.Replace(t, @"(?<=[a-z])x(?=\d)", " x");

            // 3) Separadores que NO son parte de un número decimal
            t = t.Replace("-", " ").Replace("/", " ").Replace(",", " ");
            t = Regex.Replace(t, @"(?<!\d)\.(?!\d)", " "); // puntos que no están entre dígitos

            // 4) Separar número de letra y letra de número: "200g" -> "200 g", "x1" -> "x 1"
            t = Regex.Replace(t, @"(\d+(?:\.\d+)?)([a-z]+)", "$1 $2");
            t = Regex.Replace(t, @"([a-z]+)(\d+(?:\.\d+)?)", "$1 $2");

            // 5) Colapsar espacios múltiples
            t = Regex.Replace(t, @"\s+", " ").Trim();

            return t;
        }

        private bool EsNumero(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

        // ============================================================
        // LEVENSHTEIN (distancia de edición clásica, programación dinámica)
        // ============================================================

        public static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
            if (string.IsNullOrEmpty(b)) return a.Length;

            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + costo);
                }
            }
            return d[a.Length, b.Length];
        }

        // ============================================================
        // COMPATIBILIDAD (opcional, para no romper el build durante la migración)
        // ============================================================

        [Obsolete("Usar AnalizarProducto(). Se mantiene solo para migrar gradualmente.")]
        public string[] ExtraerPalabrasClave(string nombre) =>
            AnalizarProducto(nombre).Tokens.Select(t => t.Texto).ToArray();
    }
}