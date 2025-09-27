using Client.CsvReader;
using Common;
using System;
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
                Log(logWriter, "Starting CSV processing");

                try
                {
                    var samples = CsvLoader.LoadSamplesFromCsv(csvPath, logWriter);

                    if (samples.Length == 0)
                    {
                        Console.WriteLine("No valid samples found!");
                        return;
                    }

                    Console.WriteLine($"Loaded {samples.Length} samples from CSV");
                    Log(logWriter, $"Loaded {samples.Length} samples");

                    using (var channelFactory = new ChannelFactory<IServiceContract>("WeatherService"))
                    {
                        var client = channelFactory.CreateChannel();
                        RunSession(client, samples, logWriter);
                        CloseChannel((IClientChannel)client);
                    }
                }
                catch (EndpointNotFoundException)
                {
                    Console.WriteLine("Error: Weather service is not running. Please start the server first.");
                    Log(logWriter, "Service not available");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Log(logWriter, $"Unexpected error: {ex.Message}");
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void RunSession(IServiceContract client, WeatherSample[] samples, StreamWriter logWriter)
        {
            var metadata = new SessionMetadata
            {
                StationId = "Station_1",
                StartTime = DateTime.Now,
                ExpectedSamples = samples.Length
            };

            string sessionId = client.StartSession(metadata);
            Console.WriteLine($"Session started: {sessionId}");
            Log(logWriter, $"Session started: {sessionId}");

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
            Console.WriteLine(ended ? "Session ended successfully" : "Failed to end session");
            Log(logWriter, $"Session ended: {ended}");

            Console.WriteLine($"\nSummary: {successCount} successful, {errorCount} failed");
        }

        private static bool SendSample(IServiceContract client, WeatherSample sample, int index, int total, StreamWriter logWriter)
        {
            try
            {
                Console.WriteLine($"Sending sample {index}/{total}...");
                bool success = client.PushSample(sample);

                if (success)
                {
                    Console.WriteLine("Sample sent successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine("Failed to send sample");
                    return false;
                }
            }
            catch (FaultException<ValidationFault> ex)
            {
                Log(logWriter, $"Validation error: {ex.Detail.Message}");
                Console.WriteLine($"Validation error: {ex.Detail.Message}");
            }
            catch (FaultException<DataFormatFault> ex)
            {
                Log(logWriter, $"Data format error: {ex.Detail.Message}");
                Console.WriteLine($"Data format error: {ex.Detail.Message}");
            }
            catch (Exception ex)
            {
                Log(logWriter, $"Error sending sample: {ex.Message}");
                Console.WriteLine($"Error sending sample: {ex.Message}");
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
