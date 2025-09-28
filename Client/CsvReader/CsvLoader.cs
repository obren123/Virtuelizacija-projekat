using Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.CsvReader
{
    public static class CsvLoader
    {
        public static WeatherSample[] LoadSamplesFromCsv(string csvPath, StreamWriter logWriter)
        {
                var samples = new List<WeatherSample>();

                if (!File.Exists(csvPath))
                {
                    string error = $"Nema CSV fajla: {csvPath}";
                    Console.WriteLine(error);
                    throw new FileNotFoundException(error);
                }

                int lineNumber = 0;
                int maxSamples = 100;

                try
                {
                    using (var reader = new StreamReader(csvPath))
                    {
                        reader.ReadLine(); // preskoči header
                        lineNumber++;

                        while (!reader.EndOfStream && samples.Count < maxSamples)
                        {
                            lineNumber++;
                            var line = reader.ReadLine();
                            if (string.IsNullOrEmpty(line)) continue;

                            var values = line.Split(',');

                            try
                            {
                                if (values.Length >= 10)
                                {
                                    var sample = new WeatherSample
                                    {
                                        T = ParseDouble(values[2], logWriter, lineNumber, "T"),
                                        Tpot = ParseDouble(values[3], logWriter, lineNumber, "Tpot"),
                                        Tdew = ParseDouble(values[4], logWriter, lineNumber, "Tdew"),
                                        rh = ParseDouble(values[5], logWriter, lineNumber, "rh"),
                                        sh = ParseDouble(values[9], logWriter, lineNumber, "sh"),
                                        Date = ParseDate(values[0])
                                    };

                                    samples.Add(sample);
                                    Console.WriteLine($"Ucitani podatak: {sample.Date}");
                                }
                                else
                                {
                                    logWriter.WriteLine($"Linija {lineNumber}: Nevazeci broj kolona {values.Length}");
                                }
                            }
                            catch (FormatException ex)
                            {
                                logWriter.WriteLine($"Linija {lineNumber}: Format error: {ex.Message}");
                            }
                            catch (Exception ex)
                            {
                                logWriter.WriteLine($"Linija {lineNumber}: Error: {ex.Message}");
                            }
                        }
                    }
                    Console.WriteLine($"Završi učitavanje {samples.Count} podataka");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greska pri citanju CSV fajla: {ex.Message}");
                    logWriter.WriteLine($"{DateTime.Now}: Greska pri citanju CSV fajla: {ex.Message}");
                }
                return samples.ToArray();
        }

            private static double ParseDouble(string s, StreamWriter logWriter, int lineNumber, string columnName)
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    logWriter.WriteLine($"Linija {lineNumber}: Prazna vrednost u koloni {columnName}");
                    return double.NaN;
                }

                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return value;

                logWriter.WriteLine($"Linija {lineNumber}: Nevazeci dabl '{s}' za {columnName}");
                return double.NaN;
            }

            private static DateTime ParseDate(string s)
            {
                return DateTime.TryParseExact(
                                    s,
                                    "yyyy-MM-dd HH:mm:ss",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out var d
                                ) ? d : DateTime.MinValue;
            }
    }
}


