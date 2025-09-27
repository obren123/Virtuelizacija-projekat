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
                        new DataFormatFault { Message = "StationId is required" },
                        new FaultReason("StationId is required"));

                if (meta.ExpectedSamples <= 0)
                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault { Message = "ExpectedSamples must be greater than 0" },
                        new FaultReason("Invalid expected samples"));

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
                if (string.IsNullOrEmpty(_sessionId) ||
                    !_measurementsWriters.ContainsKey(_sessionId) ||
                    !_rejectsWriters.ContainsKey(_sessionId))
                {
                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault { Message = "Session not properly initialized. Call StartSession first." },
                        new FaultReason("Session not initialized"));
                }

                var mWriter = _measurementsWriters[_sessionId];
                var rWriter = _rejectsWriters[_sessionId];

                if (sample.Date == DateTime.MinValue)
                    throw new FaultException<ValidationFault>(
                        new ValidationFault { Message = "Invalid date" },
                        new FaultReason("Invalid date"));

                if (double.IsNaN(sample.sh) || sample.sh < 0 || sample.sh > 100)
                    throw new FaultException<ValidationFault>(
                        new ValidationFault { Message = "Specific humidity must be between 0 and 100" },
                        new FaultReason("Invalid specific humidity"));

                AnalyzeSpecificHumidity(sample);
                AnalyzeHeatIndex(sample);

                mWriter.WriteLine($"{sample.T},{sample.Tpot},{sample.Tdew},{sample.sh},{sample.rh},{sample.Date:o}");
                mWriter.Flush();

                _events.RaiseOnSampleReceived(sample);

                return true;
            }
        }

        public bool EndSession(string sessionId)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(_sessionId) || _sessionId != sessionId)
                {
                    throw new FaultException<DataFormatFault>(
                        new DataFormatFault { Message = "Invalid session ID" },
                        new FaultReason("Invalid session ID"));
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

            double change = sample.sh - _previousSh;
            if (Math.Abs(change) > _shThreshold)
            {
                string trend = change > 0 ? "above" : "below";
                _events.RaiseOnWarningRaised(
                    $"SH spike detected: {Math.Abs(change):F2} ({trend} threshold)");
            }

            double minAllowed = _shMean * 0.75;
            double maxAllowed = _shMean * 1.25;

            if (sample.sh < minAllowed)
            {
                _events.RaiseOnWarningRaised(
                    $"SH too low: {sample.sh:F2} < {minAllowed:F2}");
            }
            else if (sample.sh > maxAllowed)
            {
                _events.RaiseOnWarningRaised(
                    $"SH too high: {sample.sh:F2} > {maxAllowed:F2}");
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
            double currentHi = CalculateHeatIndex(sample.T, sample.rh);

            if (double.IsNaN(_previousHi))
            {
                _previousHi = currentHi;
                return;
            }

            double difference = currentHi - _previousHi;
            if (Math.Abs(difference) > _hiThreshold)
            {
                string changeType = difference > 0 ? "above" : "below";
                _events.RaiseOnWarningRaised(
                    $"HI spike: {Math.Abs(difference):F2} ({changeType} expected)");
            }

            _previousHi = currentHi;
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
