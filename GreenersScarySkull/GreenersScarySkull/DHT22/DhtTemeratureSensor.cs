using Sensors.Dht;
using System;
using Windows.Devices.Gpio;
using Windows.System.Threading;

namespace GreenersScarySkull.DHT22
{
    public sealed class DHTTempArgs
    {
        public double Temperature { get; set; }
        public double Humid { get; set; }
        public double HeatIndex { get; set; }
        public double DewPoint { get; set; }
    }

    /// <summary>
    /// This is just a simple wrapper for the C++ one wire implementation from the MS examples.
    /// </summary>
    public sealed class DhtTemeratureSensor
    {
        private IDht _dht;
        private ThreadPoolTimer _dhtTimer;

        internal event EventHandler<DHTTempArgs> DhtValuesChanged;

        public void RunDHTSensor(int pin, GpioController gpioController, TimeSpan timeSpan)
        {
            _dht = new Dht11(gpioController.OpenPin(pin, GpioSharingMode.Exclusive), GpioPinDriveMode.Input);
            _dhtTimer = ThreadPoolTimer.CreatePeriodicTimer(OnDHTTick, timeSpan);
        }

        private async void OnDHTTick(ThreadPoolTimer timer)
        {
            DhtReading reading;
            reading = await _dht.GetReadingAsync().AsTask();

            if (!reading.IsValid)
            {
                return;
            }

            //Raise the event to the client that we have new data
            DhtValuesChanged?.Invoke(this, new DHTTempArgs
            {
                Temperature = Math.Round(Convert.ToSingle(Farehieght(reading.Temperature)), 1, MidpointRounding.AwayFromZero),
                Humid = Math.Round(Convert.ToSingle(reading.Humidity), 1, MidpointRounding.AwayFromZero),
                DewPoint = Math.Round(Convert.ToSingle(dewPointFast(reading.Temperature, reading.Humidity)), 1, MidpointRounding.AwayFromZero),
                HeatIndex = Math.Round(Convert.ToSingle(heatIndex(Farehieght(reading.Temperature), reading.Humidity)), 1, MidpointRounding.AwayFromZero)
            });
        }

        private static double dewPointFast(double celsius, double humidity)
        {
            const double a = 17.271;
            const double b = 237.7;
            double temp = (a * celsius) / (b + celsius) + Math.Log10(humidity * 0.01);
            return (b * temp) / (a - temp);
        }

        private static double Farehieght(double c) => (c * 9) / 5 + 32;

        private static double heatIndex(double tempF, double humidity)
        {
            const double c1 = -42.38, c2 = 2.049, c3 = 10.14, c4 = -0.2248, c5 = -6.838e-3,
                            c6 = -5.482e-2, c7 = 1.228e-3, c8 = 8.528e-4, c9 = -1.99e-6;
            double T = tempF;
            double R = humidity;

            double A = (c5 * T + c2) * T + c1;
            double B = (c7 * T + c4) * T + c3;
            double C = (c9 * T + c8) * T + c6;

            double rv = (C * R + B) * R + A;
            return rv;
        }
    }
}