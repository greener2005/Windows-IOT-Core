using System;
using System.Diagnostics;
using Windows.Devices.Gpio;
using GreenersScarySkull.ENUMS;

namespace GreenersScarySkull.PirMotionDetector
{
    /// <summary>
    /// This is a sample that was pulled from the MS IOT examples on using the PIR motion detector.  
    /// You will have to tweak both the de-bounce timeout as well as the calibration on the PIR sensor itself for best results in your projects.
    /// </summary>
    public sealed class PirMotionSensor
    {

        /// <summary>
        /// GpioPin object for the sensor's signal pin
        /// </summary>
        private readonly GpioPin _pirSensorPin;

        /// <summary>
        /// The edge to compare the signal with for motion based on the sensor type.
        /// </summary>
        private readonly GpioPinEdge _pirSensorEdge;

        /// <summary>
        /// Occurs when motion is detected
        /// </summary>
        public event EventHandler<GpioPinValueChangedEventArgs> MotionDetected;

        public PirMotionSensor(int sensorPin, LocalSensorType sensorType, GpioController controller)
        {


            var gpioController = controller;
            if (gpioController != null)
            {
                _pirSensorEdge = sensorType == LocalSensorType.ActiveHigh ? GpioPinEdge.FallingEdge : GpioPinEdge.RisingEdge;
                _pirSensorPin = gpioController.OpenPin(sensorPin, GpioSharingMode.Exclusive);
                _pirSensorPin.SetDriveMode(GpioPinDriveMode.Input);
                _pirSensorPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
                _pirSensorPin.ValueChanged += PirSensorPin_ValueChanged;
            }
            else
            {
                Debug.WriteLine("Error: GPIO controller not found.");
            }
        }
        /// <summary>
        /// Performs tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _pirSensorPin.Dispose();
        }
        /// <summary>
        /// Occurs when motion sensor pin value has changed
        /// </summary>
        private void PirSensorPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (MotionDetected != null && args.Edge == _pirSensorEdge)
            {
                MotionDetected(this, args);
            }
        }
    }
}
