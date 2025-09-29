using Client.CsvReader;
using System;
using Common;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using System.Threading;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var csvPath = ConfigurationManager.AppSettings["CsvFilePath"];
            var logPath = ConfigurationManager.AppSettings["LogFilePath"];

            if (!string.IsNullOrEmpty(Path.GetDirectoryName(logPath)))
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));

            using (var logWriter = new StreamWriter(logPath, true))
            {
                Log(logWriter, "Pokretanje obrade CSV fajla");

                try
                {
                    var samples = CsvLoader.LoadSamplesFromCsv(csvPath, logWriter);

                    if (samples.Length == 0)
                    {
                        Console.WriteLine("Nijedan validan uzorak nije pronađen!");
                        return;
                    }

                    Console.WriteLine($"Učitano {samples.Length} uzoraka iz CSV fajla");
                    Log(logWriter, $"Učitano {samples.Length} uzoraka");

                    //7
                    using (var channelFactory = new ChannelFactory<IServiceContract>("WeatherService"))
                    {
                        var client = channelFactory.CreateChannel();
                        RunSession(client, samples, logWriter);
                        CloseChannel((IClientChannel)client);
                    }
                }
                catch (EndpointNotFoundException)
                {
                    Console.WriteLine("Greška: Servis za vremenske podatke nije pokrenut. Pokrenite server prvo.");
                    Log(logWriter, "Servis nije dostupan");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška: {ex.Message}");
                    Log(logWriter, $"Neočekivana greška: {ex.Message}");
                }
            }

            Console.WriteLine("Pritisnite bilo koji taster za izlaz...");
            Console.ReadKey();
        }

        private static void RunSession(IServiceContract client, WeatherSample[] samples, StreamWriter logWriter)
        {
            var metadata = new SessionMetadata
            {
                StationId = "Stanica_1",
                StartTime = DateTime.Now,
                ExpectedSamples = samples.Length
            };

            string sessionId = client.StartSession(metadata);
            Console.WriteLine($"Sesija započeta: {sessionId}");
            Log(logWriter, $"Sesija započeta: {sessionId}");

            int successCount = 0;
            int errorCount = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                if (SendSample(client, samples[i], i + 1, samples.Length, logWriter))
                    successCount++;
                else
                    errorCount++;

                Thread.Sleep(100);
            }

            bool ended = client.EndSession(sessionId);
            Console.WriteLine(ended ? "Sesija uspešno završena" : "Neuspešno završavanje sesije");
            Log(logWriter, $"Sesija završena: {ended}");

            Console.WriteLine($"\nRezime: {successCount} uspešno, {errorCount} neuspešno");
        }

        private static bool SendSample(IServiceContract client, WeatherSample sample, int index, int total, StreamWriter logWriter)
        {
            try
            {
                Console.WriteLine($"Slanje uzorka {index}/{total}...");
                bool success = client.PushSample(sample);

                if (success)
                {
                    Console.WriteLine("Uzorak uspešno poslat");
                    return true;
                }
                else
                {
                    Console.WriteLine("Slanje uzorka neuspešno");
                    return false;
                }
            }
            catch (FaultException<ValidationFault> ex)
            {
                Log(logWriter, $"Greška validacije: {ex.Detail.Message}");
                Console.WriteLine($"Greška validacije: {ex.Detail.Message}");
            }
            catch (FaultException<DataFormatFault> ex)
            {
                Log(logWriter, $"Greška formata podataka: {ex.Detail.Message}");
                Console.WriteLine($"Greška formata podataka: {ex.Detail.Message}");
            }
            catch (Exception ex)
            {
                Log(logWriter, $"Greška prilikom slanja uzorka: {ex.Message}");
                Console.WriteLine($"Greška prilikom slanja uzorka: {ex.Message}");
            }
            return false;
        }

        private static void CloseChannel(IClientChannel channel)
        {
            try
            {
                if (channel.State == CommunicationState.Faulted)
                    channel.Abort();
                else
                    channel.Close();
            }
            catch
            {
                channel.Abort();
            }
        }

        private static void Log(StreamWriter writer, string message)
        {
            writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
            writer.Flush();
        }
    }
}
