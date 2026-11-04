using System.Collections.Generic;
using System.Globalization;

namespace SimuladorWeb
{
    // Las mismas fórmulas del programa de consola (SimuladorMarkov/Program.cs).
    // La diferencia: aquí las funciones NO escriben en la consola,
    // sino que devuelven su resultado para que la página web lo muestre.
    public static class Markov
    {
        // Umbral de distancia que define el mixing time
        public const double UMBRAL = 0.25;

        // Margen de error permitido al revisar que una fila sume 1
        public const double TOLERANCIA = 0.0001;

        // Número máximo de pasos al buscar el mixing time
        public const int MAX_PASOS = 10000;

        // Convierte un texto en número. Acepta "0.5", "0,5" y fracciones como "1/3".
        public static bool LeerNumero(string texto, out double valor)
        {
            texto = texto.Trim().Replace(',', '.');
            valor = 0;

            if (texto.Contains('/'))
            {
                // Fracción: arriba / abajo
                string[] partes = texto.Split('/');
                if (partes.Length != 2) return false;
                if (!double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double arriba)) return false;
                if (!double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double abajo)) return false;
                if (abajo == 0) return false;
                valor = arriba / abajo;
            }
            else
            {
                if (!double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out valor)) return false;
            }

            // Rechazar "NaN" o "Infinity", que también se pueden leer como double
            return double.IsFinite(valor);
        }

        // Copia la fila i de una matriz en un vector
        public static double[] ObtenerFila(double[,] M, int i)
        {
            int n = M.GetLength(1);
            double[] fila = new double[n];
            for (int j = 0; j < n; j++)
                fila[j] = M[i, j];
            return fila;
        }

        // ------------------------------------------------------------
        // 1. Revisa una fila: sin valores negativos y que sume 1.
        //    Devuelve "" si está bien, o un mensaje con el problema.
        // ------------------------------------------------------------
        public static string RevisarFila(double[] fila)
        {
            double suma = 0;
            for (int j = 0; j < fila.Length; j++)
            {
                if (fila[j] < 0)
                    return "tiene un valor negativo";
                suma += fila[j];
            }

            // La suma debe ser 1:  |suma - 1| < 0.0001
            if (Math.Abs(suma - 1) >= TOLERANCIA)
                return $"suma {suma:F4} y debe sumar 1";

            return "";
        }

        // Una matriz es estocástica si TODAS sus filas están bien.
        public static bool EsEstocastica(double[,] P)
        {
            int n = P.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                if (RevisarFila(ObtenerFila(P, i)) != "")
                    return false;
            }
            return true;
        }

        // ------------------------------------------------------------
        // 2. Multiplicación de matrices cuadradas:
        //    C[i, j] = Σk A[i, k] · B[k, j]
        // ------------------------------------------------------------
        public static double[,] Multiplicar(double[,] A, double[,] B)
        {
            int n = A.GetLength(0);
            double[,] C = new double[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double suma = 0;
                    for (int k = 0; k < n; k++)
                    {
                        suma += A[i, k] * B[k, j];
                    }
                    C[i, j] = suma;
                }
            }
            return C;
        }

        // Una matriz es REGULAR si alguna potencia P^k tiene todas sus entradas > 0.
        // Basta revisar k = 1, 2, ..., (n-1)² + 1.
        // En "kEncontrado" devuelve la potencia donde se cumplió.
        public static bool EsRegular(double[,] P, out int kEncontrado)
        {
            int n = P.GetLength(0);
            int maxK = (n - 1) * (n - 1) + 1;
            double[,] potencia = P;   // empieza en P^1

            for (int k = 1; k <= maxK; k++)
            {
                // Revisar si todas las entradas de P^k son positivas
                bool todasPositivas = true;
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (potencia[i, j] <= 0)
                            todasPositivas = false;
                    }
                }

                if (todasPositivas)
                {
                    kEncontrado = k;
                    return true;
                }

                potencia = Multiplicar(potencia, P);   // pasar a P^(k+1)
            }

            kEncontrado = -1;
            return false;
        }

        // ------------------------------------------------------------
        // 3. Distribución estacionaria π por el método de la potencia:
        //    se empieza con π = (1/n, ..., 1/n) y se repite
        //    π_nuevo[j] = Σi π[i] · P[i, j]   hasta que casi no cambie.
        // ------------------------------------------------------------
        public static double[] DistribucionEstacionaria(double[,] P)
        {
            int n = P.GetLength(0);

            // Vector inicial: todos los estados igual de probables
            double[] pi = new double[n];
            for (int j = 0; j < n; j++)
                pi[j] = 1.0 / n;

            double diferencia = 1;
            while (diferencia >= 1e-10)
            {
                // π_nuevo = π · P
                double[] piNuevo = new double[n];
                for (int j = 0; j < n; j++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        piNuevo[j] += pi[i] * P[i, j];
                    }
                }

                // Diferencia máxima entre π_nuevo y π
                diferencia = 0;
                for (int j = 0; j < n; j++)
                    diferencia = Math.Max(diferencia, Math.Abs(piNuevo[j] - pi[j]));

                pi = piNuevo;
            }
            return pi;
        }

        // ------------------------------------------------------------
        // 4. Distancia de variación total entre una fila y π:
        //    DistanciaTV = ½ Σj |fila[j] − π[j]|
        // ------------------------------------------------------------
        public static double DistanciaTV(double[] fila, double[] pi)
        {
            double suma = 0;
            for (int j = 0; j < fila.Length; j++)
                suma += Math.Abs(fila[j] - pi[j]);
            return suma / 2;
        }

        // Mixing time: el primer paso t en que d(t) ≤ 0.25, donde
        // d(t) = máximo, sobre todas las filas i, de DistanciaTV(P^t[i, ·], π).
        // Cada d(t) se guarda en la lista "distancias" para la gráfica.
        // Devuelve -1 si no se alcanza en MAX_PASOS pasos.
        public static int MixingTime(double[,] P, double[] pi, List<double> distancias)
        {
            int n = P.GetLength(0);
            double[,] Pt = P;   // empieza en P^1

            for (int t = 1; t <= MAX_PASOS; t++)
            {
                // d(t) = la peor distancia entre todas las filas de P^t
                double d = 0;
                for (int i = 0; i < n; i++)
                    d = Math.Max(d, DistanciaTV(ObtenerFila(Pt, i), pi));

                distancias.Add(d);

                if (d <= UMBRAL)
                    return t;

                Pt = Multiplicar(Pt, P);   // pasar a P^(t+1)
            }
            return -1;
        }
    }
}
