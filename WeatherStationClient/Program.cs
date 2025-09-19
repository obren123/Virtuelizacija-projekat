using Client.CsvReader;
using Common;
using System;
using System.Configuration;
using System.IO;
using System.ServiceModel;

namespace WeatherStationClient
{
    class Program
    {
        static void Main(string[] args)
        {
            string csvPath = ConfigurationManager.AppSettings["CsvFilePath"];
            string logPath = ConfigurationManager.AppSettings["LogFilePath"];

            Directory.CreateDirectory(Path.GetDirectoryName(logPath));

            using (StreamWriter logWriter = new StreamWriter(logPath, true))
            {
                logWriter.WriteLine($"{DateTime.Now}: Starting CSV processing");

                try
                {
                    var samples = CsvLoader.LoadSamplesFromCsv(csvPath, logWriter);

                    if (samples.Length == 0)
                    {
                        Console.WriteLine("No valid samples found!");
                        return;
                    }

                    Console.WriteLine($"Loaded {samples.Length} samples from CSV");

                    logWriter.WriteLine($"{DateTime.Now}: Loaded {samples.Length} samples");

                    using (var channelFactory = new ChannelFactory<IServiceContract>("WeatherService"))
                    {
                        var client = channelFactory.CreateChannel();

                        var metadata = new SessionMetadata
                        {
                            StationId = "Station_1",
                            StartTime = DateTime.Now,
                            ExpectedSamples = samples.Length
                        };

                        string sessionId = client.StartSession(metadata);
                        Console.WriteLine($"Session started: {sessionId}");
                        logWriter.WriteLine($"{DateTime.Now}: Session started: {sessionId}");

                        int successCount = 0;
                        int errrorCount = 0;

                        for (int i = 0; i < samples.Length; i++)
                        {
                            try
                            {
                                Console.WriteLine($"Sending sample {i + 1}/{samples.Length}...");
                                bool success = client.PushSample(samples[i]);

                                if (success)
                                {
                                    successCount++;
                                    Console.WriteLine("Sample sent successfully");
                                }
                                else
                                {
                                    errrorCount++;
                                    Console.WriteLine("Failed to send sample");
                                }
                                System.Threading.Thread.Sleep(100);
                            }
                            catch (FaultException<ValidationFault> ex)
                            {
                                errrorCount++;
                                logWriter.WriteLine($"{DateTime.Now}: Validation error: {ex.Detail.Message}");
                                Console.WriteLine($"Validation error: {ex.Detail.Message}");
                            }
                            catch (FaultException<DataFormatFault> ex)
                            {
                                errrorCount++;
                                logWriter.WriteLine($"{DateTime.Now}: Data format error: {ex.Detail.Message}");
                                Console.WriteLine($"Data format error: {ex.Detail.Message}");
                            }
                            catch (Exception ex)
                            {
                                errrorCount++;
                                logWriter.WriteLine($"{DateTime.Now}: Error sending sample: {ex.Message}");
                                Console.WriteLine($"Error sending sample: {ex.Message}");
                            }
                        }

                        bool ended = client.EndSession(sessionId);
                        Console.WriteLine(ended ? "Session ended successfully" : "Failed to end session");
                        logWriter.WriteLine($"{DateTime.Now}: Session ended: {ended}");
                        Console.WriteLine($"\nSummary: {successCount} successfull, {errrorCount} failed");
                    }
                }
                catch (EndpointNotFoundException)
                {
                    Console.WriteLine("Error: Weather service is not running. Please start the server first.");
                    logWriter.WriteLine($"{DateTime.Now}: Service not available");
                }
                catch (Exception ex)
                {
                    logWriter.WriteLine($"{DateTime.Now}: {ex.Message}");
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
