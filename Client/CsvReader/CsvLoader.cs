using Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Client.CsvReader
{
    public static class CsvLoader
    {
        /// <summary>
        /// Učita do max 100 validnih uzoraka iz CSV-a. Nevalidne linije i linije preko 100 loguju se u logWriter.
        /// Očekuje invariant culture (decimalna tačka) i datum formata "yyyy-MM-dd HH:mm:ss".
        /// </summary>
        public static WeatherSample[] LoadSamplesFromCsv(string csvPath, StreamWriter logWriter)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
                throw new ArgumentException("csvPath nije specificiran", nameof(csvPath));

            var samples = new List<WeatherSample>();

            if (!File.Exists(csvPath))
            {
                string error = $"Nema CSV fajla: {csvPath}";
                Console.WriteLine(error);
                throw new FileNotFoundException(error);
            }

            int lineNumber = 0;
            const int maxSamples = 100;
            int totalLines = 0;
            int invalidCount = 0;
            int excessCount = 0;

            try
            {
                using (var reader = new StreamReader(csvPath))
                {
                    // preskoci header (ako postoji)
                    if (!reader.EndOfStream)
                    {
                        reader.ReadLine();
                        lineNumber++;
                    }

                    while (!reader.EndOfStream)
                    {
                        lineNumber++;
                        totalLines++;
                        var line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            logWriter.WriteLine($"Linija {lineNumber}: Prazna linija");
                            invalidCount++;
                            continue;
                        }

                        var values = line.Split(',');

                        if (values.Length < 10)
                        {
                            logWriter.WriteLine($"Linija {lineNumber}: Nevazeci broj kolona {values.Length}");
                            invalidCount++;
                            continue;
                        }

                        // Parsiraj polja (ne dodaj dok ne potvrdimo da su obavezna polja validna)
                        bool dateOk = TryParseDate(values[0], out DateTime date);
                        bool tOk = TryParseDouble(values[2], out double T);
                        bool tpotOk = TryParseDouble(values[3], out double Tpot); // opcionalno može biti neobavezno
                        bool tdewOk = TryParseDouble(values[4], out double Tdew); // opcionalno
                        bool rhOk = TryParseDouble(values[5], out double rh);
                        bool shOk = TryParseDouble(values[9], out double sh);

                        // Definišemo obavezna polja: Date, T, rh, sh
                        if (!dateOk)
                        {
                            logWriter.WriteLine($"Linija {lineNumber}: Neispravan datum '{values[0]}'");
                            invalidCount++;
                            continue;
                        }
                        if (!tOk)
                        {
                            logWriter.WriteLine($"Linija {lineNumber}: Neispravan T '{values[2]}'");
                            invalidCount++;
                            continue;
                        }
                        if (!rhOk)
                        {
                            logWriter.WriteLine($"Linija {lineNumber}: Neispravan rh '{values[5]}'");
                            invalidCount++;
                            continue;
                        }
                        if (!shOk)
                        {
                            logWriter.WriteLine($"Linija {lineNumber}: Neispravan sh '{values[9]}'");
                            invalidCount++;
                            continue;
                        }

                        // Ako već imamo 100 validnih uzoraka, prijavi kao "red viška" ali nastavi čitanje
                        if (samples.Count >= maxSamples)
                        {
                            excessCount++;
                            //logWriter.WriteLine($"Linija {lineNumber}: Red viška (preko {maxSamples})");
                            continue;
                        }

                        // Svi obavezni su ok -> dodaj uzorak
                        var sample = new WeatherSample
                        {
                            Date = date,
                            T = T,
                            Tpot = tpotOk ? Tpot : double.NaN,
                            Tdew = tdewOk ? Tdew : double.NaN,
                            rh = rh,
                            sh = sh
                        };

                        samples.Add(sample);
                        Console.WriteLine($"Ucitani podatak ({samples.Count}): {sample.Date:yyyy-MM-dd HH:mm:ss}");
                    } // kraj while
                } // using reader

                Console.WriteLine($"Završi učitavanje: učitano {samples.Count} validnih od ukupno {totalLines} linija (nevalidnih: {invalidCount}, viška: {excessCount})");
                logWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Učitano {samples.Count} validnih od ukupno {totalLines} linija (nevalidnih: {invalidCount}, viška: {excessCount})");
                logWriter.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greska pri citanju CSV fajla: {ex.Message}");
                logWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Greska pri citanju CSV fajla: {ex.Message}");
                logWriter.Flush();
            }

            return samples.ToArray();
        }

        private static bool TryParseDouble(string s, out double value)
        {
            value = double.NaN;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return double.TryParse(s, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParseDate(string s, out DateTime d)
        {
            // format "yyyy-MM-dd HH:mm:ss" kako si ranije koristio
            return DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }
    }
}
