using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SimuladorMarkov
{
    class Program
    {
        // Umbral de distancia que define el mixing time
        const double UMBRAL = 0.25;

        // Margen de error permitido al revisar que una fila sume 1
        const double TOLERANCIA = 0.0001;

        // Número máximo de pasos al buscar el mixing time
        const int MAX_PASOS = 10000;

        static void Main(string[] args)
        {
            // Para que se vean bien las tildes y el carácter █
            Console.OutputEncoding = Encoding.UTF8;
            // Para que los números se muestren con punto decimal (0.8333 y no 0,8333)
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            string respuesta = "s";
            while (respuesta == "s")
            {
                Console.WriteLine();
                Console.WriteLine("====================================================");
                Console.WriteLine("   SIMULADOR DE CADENAS DE MARKOV - MIXING TIME");
                Console.WriteLine("====================================================");

                // 1. Leer la matriz (cada fila se valida al escribirla)
                double[,] P = LeerMatriz();
                if (EsEstocastica(P))
                {
                    Console.WriteLine("\nLa matriz es estocástica.");
                }

                // 2. Revisar si la matriz es regular
                if (EsRegular(P))
                {
                    // 3. Calcular la distribución estacionaria π
                    double[] pi = DistribucionEstacionaria(P);

                    // 4. Calcular el mixing time (y guardar cada d(t) en la lista)
                    List<double> distancias = new List<double>();
                    MixingTime(P, pi, distancias);

                    // 5. Mostrar cómo converge la cadena paso a paso
                    MostrarConvergencia(distancias);
                }
                else
                {
                    Console.WriteLine("No se puede alcanzar un estado estable, así que no se calcula π ni el mixing time.");
                }

                Console.Write("\n¿Desea evaluar otra matriz? (s/n): ");
                respuesta = (Console.ReadLine() ?? "").Trim().ToLower();
            }

            Console.WriteLine("\n¡Hasta luego!");
        }

        // ------------------------------------------------------------
        // 1. Lee la matriz de transición P desde la consola.
        //    P[i, j] = probabilidad de pasar del estado i al estado j.
        // ------------------------------------------------------------
        static double[,] LeerMatriz()
        {
            // Pedir el número de estados hasta que sea un entero válido
            int n = 0;
            while (n < 1)
            {
                Console.Write("\n¿Cuántos estados tiene la cadena? ");
                string texto = Console.ReadLine() ?? "";
                if (!int.TryParse(texto, out n) || n < 1)
                {
                    Console.WriteLine("Error: escriba un número entero mayor que 0.");
                    n = 0;
                }
            }

            double[,] P = new double[n, n];
            Console.WriteLine($"\nEscriba cada fila con {n} números separados por espacios (ej: 0.9 0.1).");

            for (int i = 0; i < n; i++)
            {
                bool filaCorrecta = false;
                while (!filaCorrecta)   // se repite hasta que la fila esté bien
                {
                    Console.Write($"Fila {i + 1}: ");
                    string linea = Console.ReadLine() ?? "";
                    string[] partes = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (partes.Length != n)
                    {
                        Console.WriteLine($"Error: la fila {i + 1} debe tener exactamente {n} números.");
                    }
                    else
                    {
                        // Convertir cada texto a número (InvariantCulture: el decimal es el punto)
                        bool sonNumeros = true;
                        for (int j = 0; j < n; j++)
                        {
                            if (double.TryParse(partes[j], NumberStyles.Float, CultureInfo.InvariantCulture, out double valor))
                                P[i, j] = valor;
                            else
                                sonNumeros = false;
                        }

                        if (!sonNumeros)
                            Console.WriteLine($"Error: la fila {i + 1} tiene algo que no es un número (use punto decimal).");
                        else
                            filaCorrecta = FilaValida(P, i);   // revisa negativos y que sume 1
                    }
                }
            }
            return P;
        }

        // Revisa una sola fila i: sin valores negativos y que sume 1.
        // Si está mal, dice cuál es el problema.
        static bool FilaValida(double[,] P, int i)
        {
            int n = P.GetLength(0);
            double suma = 0;

            for (int j = 0; j < n; j++)
            {
                if (P[i, j] < 0)
                {
                    Console.WriteLine($"Error: la fila {i + 1} tiene un valor negativo. Escríbala de nuevo.");
                    return false;
                }
                suma += P[i, j];
            }

            // La suma debe ser 1:  |suma - 1| < 0.0001
            if (Math.Abs(suma - 1) >= TOLERANCIA)
            {
                Console.WriteLine($"Error: la fila {i + 1} suma {suma:F4} y debe sumar 1. Escríbala de nuevo.");
                return false;
            }
            return true;
        }

        // Una matriz es estocástica si TODAS sus filas son válidas.
        static bool EsEstocastica(double[,] P)
        {
            int n = P.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                if (!FilaValida(P, i))
                    return false;
            }
            return true;
        }

        // ------------------------------------------------------------
        // 2. Multiplicación de matrices cuadradas:
        //    C[i, j] = Σk A[i, k] · B[k, j]
        // ------------------------------------------------------------
        static double[,] Multiplicar(double[,] A, double[,] B)
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
        static bool EsRegular(double[,] P)
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
                    Console.WriteLine($"La matriz ES REGULAR: P^{k} tiene todas sus entradas > 0.");
                    Console.WriteLine("Por lo tanto, sí tiene un estado estable.");
                    return true;
                }

                potencia = Multiplicar(potencia, P);   // pasar a P^(k+1)
            }

            Console.WriteLine($"La matriz NO es regular: ninguna potencia de P^1 a P^{maxK} tiene todas sus entradas > 0.");
            return false;
        }

        // ------------------------------------------------------------
        // 3. Distribución estacionaria π por el método de la potencia:
        //    se empieza con π = (1/n, ..., 1/n) y se repite
        //    π_nuevo[j] = Σi π[i] · P[i, j]   hasta que casi no cambie.
        // ------------------------------------------------------------
        static double[] DistribucionEstacionaria(double[,] P)
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

            Console.WriteLine("\n--- DISTRIBUCIÓN ESTACIONARIA (π) ---");
            for (int j = 0; j < n; j++)
                Console.WriteLine($"Estado {j + 1}: {pi[j]:F4}");

            return pi;
        }

        // ------------------------------------------------------------
        // 4. Distancia de variación total entre una fila y π:
        //    DistanciaTV = ½ Σj |fila[j] − π[j]|
        // ------------------------------------------------------------
        static double DistanciaTV(double[] fila, double[] pi)
        {
            double suma = 0;
            for (int j = 0; j < fila.Length; j++)
                suma += Math.Abs(fila[j] - pi[j]);
            return suma / 2;
        }

        // Mixing time: el primer paso t en que d(t) ≤ 0.25, donde
        // d(t) = máximo, sobre todas las filas i, de DistanciaTV(P^t[i, ·], π).
        // Cada d(t) se guarda en la lista "distancias" para la gráfica.
        static int MixingTime(double[,] P, double[] pi, List<double> distancias)
        {
            int n = P.GetLength(0);
            double[,] Pt = P;   // empieza en P^1

            for (int t = 1; t <= MAX_PASOS; t++)
            {
                // d(t) = la peor distancia entre todas las filas de P^t
                double d = 0;
                for (int i = 0; i < n; i++)
                {
                    // Copiar la fila i de P^t en un vector
                    double[] fila = new double[n];
                    for (int j = 0; j < n; j++)
                        fila[j] = Pt[i, j];

                    d = Math.Max(d, DistanciaTV(fila, pi));
                }
                distancias.Add(d);

                if (d <= UMBRAL)
                {
                    Console.WriteLine($"\n--- MIXING TIME ---");
                    Console.WriteLine($"Mixing time = {t}  (d({t}) = {d:F4} ≤ {UMBRAL:F2})");
                    return t;
                }

                Pt = Multiplicar(Pt, P);   // pasar a P^(t+1)
            }

            Console.WriteLine($"\nNo se alcanzó d(t) ≤ {UMBRAL:F2} en {MAX_PASOS} pasos.");
            return -1;
        }

        // ------------------------------------------------------------
        // 5. Funcionalidad propia: tabla con d(t) en cada paso y una
        //    barra de █ proporcional a d(t) (40 caracteres = distancia 1).
        //    La línea | marca el umbral 0.25.
        // ------------------------------------------------------------
        static void MostrarConvergencia(List<double> distancias)
        {
            const int ANCHO = 40;                          // 40 caracteres = distancia 1
            int posicionUmbral = (int)(UMBRAL * ANCHO);    // 0.25 · 40 = columna 10

            Console.WriteLine("\n--- CONVERGENCIA HACIA π ---");
            Console.WriteLine("Cada █ vale 0.025. La línea | marca el umbral 0.25.\n");

            // Encabezado con una regla: 0 al inicio, 0.25 en el umbral y 1 al final
            string regla = "0" + new string(' ', posicionUmbral - 1) + "0.25"
                         + new string(' ', ANCHO - posicionUmbral - 5) + "1";
            Console.WriteLine("    t |   d(t)  | " + regla);
            Console.WriteLine("------+---------+-" + new string('-', ANCHO));

            for (int t = 1; t <= distancias.Count; t++)
            {
                double d = distancias[t - 1];
                int largo = (int)Math.Round(d * ANCHO);   // cuántos █ dibujar

                // Construir la barra caracter por caracter
                string barra = "";
                for (int c = 0; c < ANCHO; c++)
                {
                    if (c < largo)
                        barra += "█";
                    else if (c == posicionUmbral)
                        barra += "|";      // marca del umbral
                    else
                        barra += " ";
                }

                // Señalar el paso donde se cruza el umbral
                string nota = "";
                if (d <= UMBRAL)
                    nota = "  <-- cruza el umbral (mixing time)";

                Console.WriteLine($"{t,5} | {d:F4}  | {barra}{nota}");
            }
        }
    }
}
