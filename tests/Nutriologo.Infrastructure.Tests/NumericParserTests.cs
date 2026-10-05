using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Nutriologo.Tests.NumericParsing
{
    public class NumericParserTests
    {
        [Theory]
        [InlineData("1,240", 1240.0)]       // miles con coma
        [InlineData("1.240", 1240.0)]       // miles con punto
        [InlineData("1 240", 1240.0)]       // miles con espacio
        [InlineData("1240", 1240.0)]        // sin separador
        [InlineData("113.7", 113.7)]        // decimal con punto
        [InlineData("113,7", 113.7)]        // decimal con coma
        [InlineData("113.70", 113.7)]       // decimal con ceros trailing
        [InlineData("0.5", 0.5)]            // decimal simple
        [InlineData("0,5", 0.5)]            // decimal con coma
        [InlineData("1,234,567", 1234567.0)] // miles múltiples
        [InlineData("1.234.567", 1234567.0)] // miles múltiples con punto
        [InlineData("3,14159", 3.14159)]    // decimal muchos decimales
        [InlineData("2.71828", 2.71828)]    // decimal con punto
        public void ParseNumber_HandlesVariousFormats(string input, double expected)
        {
            var resultados = ExtraerNumeros(input);
            Assert.Single(resultados);
            Assert.Equal(expected, resultados[0], 5); // precisión 5 decimales
        }

        [Fact]
        public void ParseNumber_ExtractsMultipleNumbers()
        {
            var input = "Llevas 1,240 kcal de 2,100 kcal, te quedan 860 kcal y comiste 2 veces";
            var resultados = ExtraerNumeros(input);
            
            Assert.Equal(4, resultados.Length);
            Assert.Contains(1240.0, resultados);
            Assert.Contains(2100.0, resultados);
            Assert.Contains(860.0, resultados);
            Assert.Contains(2.0, resultados);
        }

        [Fact]
        public void ParseNumber_HandlesDecimalWeights()
        {
            var input = "Tu peso es 113.7 kg y ayer era 114,2 kg";
            var resultados = ExtraerNumeros(input);
            
            Assert.Equal(2, resultados.Length);
            Assert.Contains(113.7, resultados);
            Assert.Contains(114.2, resultados);
        }

        [Fact]
        public void ParseNumber_DistinguishesThousandsFromDecimal()
        {
            // 3 dígitos después del separador = miles (si no hay otro separador)
            Assert.Equal(1240.0, ExtraerNumeros("1,240")[0]);
            Assert.Equal(1240.0, ExtraerNumeros("1.240")[0]);
            Assert.Equal(1240.0, ExtraerNumeros("1 240")[0]);
            
            // 1-2 dígitos después = decimal
            Assert.Equal(113.7, ExtraerNumeros("113.7")[0]);
            Assert.Equal(113.7, ExtraerNumeros("113,7")[0]);
            Assert.Equal(113.7, ExtraerNumeros("113.70")[0]);
            
            // 0.5, 0,5
            Assert.Equal(0.5, ExtraerNumeros("0.5")[0]);
            Assert.Equal(0.5, ExtraerNumeros("0,5")[0]);
        }

        // Copia del método privado para testearlo.
        // DEUDA: esto verifica la copia, no el código productivo. Si cambia la
        // heurística en OllamaConversationalResponder, hay que copiarla acá a
        // mano. Lo correcto sería internal + InternalsVisibleTo.
        private static double[] ExtraerNumeros(string texto)
        {
            var matches = Regex.Matches(
                texto,
                @"(?<!\w)(?:\d{1,3}(?:[., ]\d{3})*(?:[.,]\d+)?|\d+[.,]\d+|\d+)(?!\w)"
            );

            var resultados = new List<double>();
            foreach (Match m in matches)
            {
                var s = m.Value.Replace(" ", "").Replace("_", "");
                string limpio;
                if (Regex.IsMatch(s, @"^\d{1,3}([.,]\d{3})+$"))
                {
                    limpio = Regex.Replace(s, @"[.,]", "");
                }
                else
                {
                    var partes = s.Split(new[] { ',', '.' }, StringSplitOptions.None);
                    if (partes.Length == 2 && partes[1].Length == 3 && !s.Contains("."))
                    {
                        limpio = s.Replace(",", "");
                    }
                    else if (partes.Length == 2 && partes[1].Length == 3 && !s.Contains(","))
                    {
                        limpio = s.Replace(".", "");
                    }
                    else
                    {
                        limpio = s.Replace(",", ".");
                    }
                }

                if (double.TryParse(limpio, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                    resultados.Add(val);
            }
            return resultados.ToArray();
        }
    }
}