using Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using Server.Events;
using Server.Utils;

namespace Server.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class WeatherService : IServiceContract, IDisposable
    {
        private string _sessionId;
        private string _dataDirectory;
        private bool _disposed = false;

        private readonly WeatherEvents _events = new WeatherEvents();

        private double _previousSh = double.NaN;
        private double _shMean = 0;
        private int _sampleCount = 0;
        private double _shThreshold;

        private double _previousHi = double.NaN;
        private double _hiThreshold;

        private readonly object _lockObject = new object();
        private readonly Dictionary<string, StreamWriter> _measurementsWriters = new Dictionary<string, StreamWriter>();
        private readonly Dictionary<string, StreamWriter> _rejectsWriters = new Dictionary<string, StreamWriter>();

        public event EventHandler<string> OnTransferStarted
        {
            add { _events.OnTransferStarted += value; }
            remove { _events.OnTransferStarted -= value; }
        }

        public event EventHandler<WeatherSample> OnSampleReceived
        {
            add { _events.OnSampleReceived += value; }
            remove { _events.OnSampleReceived -= value; }
        }

        public event EventHandler<string> OnTransferCompleted
        {
            add { _events.OnTransferCompleted += value; }
            remove { _events.OnTransferCompleted -= value; }
        }

        public event EventHandler<string> OnWarningRaised
        {
            add { _events.OnWarningRaised += value; }
            remove { _events.OnWarningRaised -= value; }
        }
        public event EventHandler<string> OnSHSpike
        {
            add { _events.SHSpike += value; }
            remove { _events.SHSpike -= value; }
        }

        public event EventHandler<string> OnHISpike
        {
            add { _events.HISpike += value; }
            remove { _events.HISpike -= value; }
        }
        public event EventHandler<string> OnOutOfBandWarning
        {
            add { _events.OutOfBandWarning += value; }
            remove { _events.OutOfBandWarning -= value; }
        }

        public WeatherService()
        {
            _dataDirectory = ConfigurationManager.AppSettings["DataDirectory"];
            _shThreshold = double.Parse(ConfigurationManager.AppSettings["SH_threshold"]);
            _hiThreshold = double.Parse(ConfigurationManager.AppSettings["HI_max_threshold"]);
            Directory.CreateDirectory(_dataDirectory);
        }

        public string StartSession(SessionMetadata meta)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(meta.StationId))
                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault { Message = "ID stanice je obavezan" },
                        new FaultReason("StationId je obavezan"));

                if (meta.ExpectedSamples <= 0)
                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault { Message = "Broj očekivanih uzoraka mora biti veći od 0" },
                        new FaultReason("Neispravan broj uzoraka"));

                _sessionId = $"{meta.StationId}_{meta.StartTime:yyyyMMdd_HHmmss}";

                FileHelpers.CreateWriters(_dataDirectory, _sessionId, out var mWriter, out var rWriter);

                _measurementsWriters[_sessionId] = mWriter;
                _rejectsWriters[_sessionId] = rWriter;

                _events.RaiseOnTransferStarted(_sessionId);

                return _sessionId;
            }
        }

        public bool PushSample(WeatherSample sample)
        {
            lock (_lockObject)
            {
                try
                {
                    if (string.IsNullOrEmpty(_sessionId) ||
                        !_measurementsWriters.ContainsKey(_sessionId) ||
                        !_rejectsWriters.ContainsKey(_sessionId))
                    {
                        throw new FaultException<DataFormatFault>(
                            new DataFormatFault { Message = "Sesija nije ispravno inicijalizovana. Pozovite StartSession prvo." },
                            new FaultReason("Sesija nije inicijalizovana"));
                    }

                    var mWriter = _measurementsWriters[_sessionId];
                    var rWriter = _rejectsWriters[_sessionId];

                    if (sample.Date == DateTime.MinValue)
                    {
                        rWriter.WriteLine($"{sample.T},{sample.Tpot},{sample.Tdew},{sample.sh},{sample.rh},{sample.Date:o},Neispravan datum");
                        rWriter.Flush();

                        throw new FaultException<ValidationFault>(
                            new ValidationFault { Message = "Neispravan datum" },
                            new FaultReason("Neispravan datum"));
                    }

                    if (double.IsNaN(sample.sh) || sample.sh < 0 || sample.sh > 100)
                    {
                        rWriter.WriteLine($"{sample.T},{sample.Tpot},{sample.Tdew},{sample.sh},{sample.rh},{sample.Date:o},Specifična vlažnost mora biti između 0 i 100");
                        rWriter.Flush();

                        throw new FaultException<ValidationFault>(
                            new ValidationFault { Message = "Specifična vlažnost mora biti između 0 i 100" },
                            new FaultReason("Neispravna specifična vlažnost"));
                    }

                    AnalyzeSpecificHumidity(sample);
                    AnalyzeHeatIndex(sample);

                    mWriter.WriteLine($"{sample.T},{sample.Tpot},{sample.Tdew},{sample.sh},{sample.rh},{sample.Date:o}");
                    mWriter.Flush();

                    _events.RaiseOnSampleReceived(sample);

                    return true;
                }
                catch (FaultException)
                {
                    // FaultException ide dalje, ali reject je već zapisan gore u validaciji
                    throw;
                }
                catch (Exception ex)
                {
                    // Ostale greške idu u rejects
                    if (!string.IsNullOrEmpty(_sessionId) && _rejectsWriters.ContainsKey(_sessionId))
                    {
                        _rejectsWriters[_sessionId].WriteLine(
                            $"{sample.T},{sample.Tpot},{sample.Tdew},{sample.sh},{sample.rh},{sample.Date:o},{ex.Message}");
                        _rejectsWriters[_sessionId].Flush();
                    }

                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault { Message = ex.Message },
                        new FaultReason(ex.Message));
                }
            }
        }


        public bool EndSession(string sessionId)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(_sessionId) || _sessionId != sessionId)
                {
                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault { Message = "Neispravan ID sesije" },
                        new FaultReason("Neispravan ID sesije"));
                }

                FileHelpers.CloseWriters(_measurementsWriters, _rejectsWriters, sessionId);

                _events.RaiseOnTransferCompleted(sessionId);

                return true;
            }
        }

        private void AnalyzeSpecificHumidity(WeatherSample sample)
        {
            _sampleCount++;
            _shMean += (sample.sh - _shMean) / _sampleCount;

            if (double.IsNaN(_previousSh))
            {
                _previousSh = sample.sh;
                return;
            }

            double deltaSH = sample.sh - _previousSh;

            // 1) Nagli skok SH → SHSpike
            if (Math.Abs(deltaSH) > _shThreshold)
            {
                string trend = deltaSH > 0 ? "iznad" : "ispod";
                _events.RaiseSHSpike($"SHSpike detektovan: ΔSH = {deltaSH:F2} ({trend} očekivanog)");
            }

            // 2) Tekući prosek ±25% → OutOfBandWarning
            double minAllowed = _shMean * 0.75;
            double maxAllowed = _shMean * 1.25;

            if (sample.sh < minAllowed)
            {
                _events.RaiseOutOfBandWarning($"SH preniska: {sample.sh:F2} < {minAllowed:F2}");
            }
            else if (sample.sh > maxAllowed)
            {
                _events.RaiseOutOfBandWarning($"SH previsoka: {sample.sh:F2} > {maxAllowed:F2}");
            }

            _previousSh = sample.sh;
        }



        private double CalculateHeatIndex(double temperature, double humidity)
        {
            return -8.78 + 1.61 * temperature + 2.34 * humidity
                - 0.15 * temperature * humidity - 0.01 * Math.Pow(temperature, 2)
                - 0.02 * Math.Pow(humidity, 2);
        }

        private void AnalyzeHeatIndex(WeatherSample sample)
        {
            lock (_lockObject)
            {
                try
                {
                    double currentHi = CalculateHeatIndex(sample.T, sample.rh);

                    if (double.IsNaN(_previousHi))
                    {
                        _previousHi = currentHi;
                        return;
                    }

                    double deltaHI = currentHi - _previousHi;

                    if (Math.Abs(deltaHI) > _hiThreshold)
                    {
                        string trend = deltaHI > 0 ? "iznad" : "ispod";
                        _events.RaiseHISpike($"HISpike detektovan: ΔHI = {deltaHI:F2} ({trend} očekivanog)");
                    }

                    _previousHi = currentHi;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška u AnalyzeHeatIndex: {ex.Message}");
                }
            }
        }




        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    FileHelpers.DisposeAll(_measurementsWriters, _rejectsWriters);
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~WeatherService()
        {
            Dispose(false);
        }
    }
}
