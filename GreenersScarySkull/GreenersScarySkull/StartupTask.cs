namespace GreenersScarySkull
{
    using BlueMix;
    using DHT22;
    using ENUMS;
    using MCP3008;
    using Microsoft.IoT.Lightning.Providers;
    using Newtonsoft.Json;
    using PirMotionDetector;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.ApplicationModel.Background;
    using Windows.Devices;
    using Windows.Devices.Gpio;
    using Windows.Devices.Pwm;
    using Windows.Media.Core;
    using Windows.Media.Playback;
    using Windows.System.Threading;

    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private GpioController _gpioController;
        private PwmController _pwmController;
        private ThreadPoolTimer _servotimer;
        private ThreadPoolTimer _eyesLedTimer;
        private ThreadPoolTimer _relaySwitchTimer;
        private PwmPin _led1PwmPin;
        private PwmPin _led2PwmPin;
        private PwmPin _led3PwmPin;
        private PwmPin _motorPin;

        // private StorageFile _wavFile;
        private MediaPlayer _mediaPlayer;

        private PirMotionSensor _pirMotionSensor;
        private CancellationTokenSource _greenLedPwmCancellationToken;
        private CancellationTokenSource _dhtCancellationTokenSource;
        private MCP3008DirectMemoryMapDriver _mcp3008;
        private DhtTemeratureSensor _dhtSensor;
        private DeviceClient _bluemixDeviceClient;

        private GpioPin _serialDigitalInputPin; 
        private GpioPin _registerClockPin; 
        private GpioPin _serialClockPin; 
        private GpioPin _leftEyeLed;
        private GpioPin _rightEyeLed;
        private GpioPin _relaySwitchPin;
        private double _clockwisePulseLength = 1;
        private double _counterClockwisePulseLegnth = 2;
        private double _restingPulseLegnth = 0;
        private double _currentPulseLength;
        private double _secondPulseLength;
        private int _iteration;

        private readonly byte[] LED =
        {
            0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80
        };

        private bool _turnOffLEDS;

        private readonly string[] WAV_FILES =
        {
            "ms-appx:///Assets/Evil_Laugh_1-Timothy-64737261.wav",
            "ms-appx:///Assets/Creepy_Laugh-Adam_Webb-235643261.wav",
            "ms-appx:///Assets/Dark_Laugh-HopeinAwe-1491150192.wav",
            "ms-appx:///Assets/Demon_Girls_Mockingbir-Hello-1365708396.wav",
            "ms-appx:///Assets/Demon_Your_Soul_is_mine-BlueMann-1903732045.wav",
            "ms-appx:///Assets/Evil_Laugh_Male_6-Himan-1359990674.wav",
            "ms-appx:///Assets/Evil_laugh_Male_9-Himan-1598312646.wav",
            "ms-appx:///Assets/Godzilla_Roar-Marc-1912765428.wav",
            "ms-appx:///Assets/I_will_kill_you-Grandpa-13673816.wav",
            "ms-appx:///Assets/Little_Demon_Girl_Song-KillahChipmunk-2101926733.wav",
            "ms-appx:///Assets/Maniacal Witches Laugh-SoundBible.com-262127569.wav",
            "ms-appx:///Assets/Sick_Villain-Peter_De_Lang-1465872262.wav"
        };

        /// <summary>The main program loop for a background IOT process on the PI</summary>
        /// <param name="taskInstance"></param>
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            //Deferral for background tasks.
            _deferral = taskInstance.GetDeferral();

            //Start up the Blue mix cloud
            _dhtCancellationTokenSource = new CancellationTokenSource();
            InitCloudConnection();

            //Make sure DMAP is configured on the PI
            VerifyLightningIsEnabledOnDevice();
            //Init the GPIO for lighting
            InitializeGPIO();
            //Init the PWN (This takes a while, so we have to call this after we pull out the control (softPWM)
            _pwmController = InitializePulseWidthModulation().Result;
            //Init the PWM pins.
            InitPWMPins();
            //init the LEDS
            InitLeds();
            //Init the shift register pins
            InitShiftRegister();

            //shut the leds down
            TurnOffLeds();

            //Init the media player
            Task.Run(() => InitializeMediaPlayer());

            //Init the motion detector
            Task.Run(() => InitPirMotionSensor());

            //Init the analog light sensor
            Task.Run(() => InitLightSensor());
            //Init the temp sensor
            Task.Factory.StartNew(InitDHT, _dhtCancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }

        private void InitCloudConnection()
        {
            _bluemixDeviceClient = new DeviceClient("pqhrnz", "greenersrpi3", "a12d1dc72c0f90ff238bcd545878508ec9d3",
                "IeOlKUvq(vNcKoB+)G");
            _bluemixDeviceClient.connect();
        }

        private void InitPWMPins()
        {
            _motorPin = _pwmController.OpenPin(5);
            _led1PwmPin = _pwmController.OpenPin(26);
            _led2PwmPin = _pwmController.OpenPin(6);
            _led3PwmPin = _pwmController.OpenPin(17);

            _pwmController.SetDesiredFrequency(250);
            _motorPin.SetActiveDutyCyclePercentage(_restingPulseLegnth);
            _led1PwmPin.SetActiveDutyCyclePercentage(_restingPulseLegnth);
            _led2PwmPin.SetActiveDutyCyclePercentage(_restingPulseLegnth);
            _led3PwmPin.SetActiveDutyCyclePercentage(_restingPulseLegnth);
        }

        /// <summary>
        /// Make sure the user has the PI set to Direct Memory Map instead of the default Inbox
        /// Driver in devices.
        /// </summary>
        private static void VerifyLightningIsEnabledOnDevice()
        {
            //Get the lighting provider that will take advantage of the DMP on the PI
            if (LightningProvider.IsLightningEnabled)
                LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
            else
                throw new Exception(
                    "Lightning is NOT configured, please put your PI to use the Direct Memory Map driver in Devices!");
        }

        private void InitLeds()
        {
            _leftEyeLed = _gpioController.OpenPin(20);
            _leftEyeLed.SetDriveMode(GpioPinDriveMode.Output);
            _leftEyeLed.Write(GpioPinValue.Low);

            _rightEyeLed = _gpioController.OpenPin(16);
            _rightEyeLed.SetDriveMode(GpioPinDriveMode.Output);
            _rightEyeLed.Write(GpioPinValue.Low);
            //_relaySwitchPin = _gpioController.OpenPin(3);
            //_relaySwitchPin.Write(GpioPinValue.Low);
            //_relaySwitchPin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private void InitShiftRegister()
        {
            _serialDigitalInputPin = _gpioController.OpenPin(22);
            _serialDigitalInputPin.SetDriveMode(GpioPinDriveMode.Output);

            _registerClockPin = _gpioController.OpenPin(18);
            _registerClockPin.SetDriveMode(GpioPinDriveMode.Output);

            _serialClockPin = _gpioController.OpenPin(27);
            _serialClockPin.SetDriveMode(GpioPinDriveMode.Output);

            ShifIn(0x00);
            PulseRegisterClock();
        }

        private void InitLightSensor()
        {
            _mcp3008 = new MCP3008DirectMemoryMapDriver();
            _mcp3008.LightingValueChanged += OnLightingValueChanged;
            _mcp3008.RunAdcChannel(TimeSpan.FromMilliseconds(1000));
        }

        private void InitDHT()
        {
            _dhtSensor = new DhtTemeratureSensor();
            _dhtSensor.RunDHTSensor(19, _gpioController, TimeSpan.FromSeconds(5));
            _dhtSensor.DhtValuesChanged += OnDhtValuesChanged;
        }

        private void InitPirMotionSensor()
        {
            _pirMotionSensor = new PirMotionSensor(21, LocalSensorType.ActiveLow, _gpioController);
            _pirMotionSensor.MotionDetected += OnMotionDetected;
        }

        private async Task<PwmController> InitializePulseWidthModulation()
        {
            //Since I don't have the PCA chip, we can get away with using the softPWN which is actually pretty fast with
            //the lighting providers.  We are only using one servo so the load and response time is good enough for what we are
            //doing with the servo.
            try
            {
                IReadOnlyList<PwmController> pwmControllers =
                    await PwmController.GetControllersAsync(LightningPwmProvider.GetPwmProvider());

                return pwmControllers[1]; //software PWM
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(InitializePulseWidthModulation)} threw this exception: {ex.Message}");
                return null;
            }
        }

        private void InitializeMediaPlayer()
        {
            _mediaPlayer = BackgroundMediaPlayer.Current;
            _mediaPlayer.MediaEnded += OnWaveFilefinishedPlaying;
            _mediaPlayer.AutoPlay = false;
        }

        private async void InitializeGPIO() => _gpioController = await GpioController.GetDefaultAsync();

        private void OnServoTick(ThreadPoolTimer timer)
        {
            _iteration++;
            if (_iteration % 3 == 0)
            {
                _currentPulseLength = _clockwisePulseLength;
                _secondPulseLength = _counterClockwisePulseLegnth;
            }
            else if (_iteration % 3 == 1)
            {
                _currentPulseLength = _counterClockwisePulseLegnth;
                _secondPulseLength = _clockwisePulseLength;
            }
            else
            {
                _currentPulseLength = 0;
                _secondPulseLength = 0;
            }

            double desiredPercentage = _currentPulseLength / (1200 / _pwmController.ActualFrequency);
            _motorPin.SetActiveDutyCyclePercentage(desiredPercentage);
        }

        private void OnLightingValueChanged(object sender, LightingValueArgs e)
        {
            _bluemixDeviceClient?.publishEvent("LightSensorEvent", "json", JsonConvert.SerializeObject(e));
            Debug.WriteLine($"Lighting Value: {e.LightValue}");
        }

        private async void OnMotionDetected(object sender, GpioPinValueChangedEventArgs e)
        {
            //Only detect motion when we are not already playing the audio.
            if (!_turnOffLEDS)
            {
                Debug.WriteLine("IGNORING PIR MOTION, AUDIO FILE IS STILL PLAYING");
                return;
            }
            try
            {
                _greenLedPwmCancellationToken = new CancellationTokenSource();

                _turnOffLEDS = false;
                int randomWaveFile = new Random(DateTime.Now.Millisecond).Next(0, 5);
                _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(WAV_FILES[randomWaveFile]));
                Debug.WriteLine(
                    $"!!!!!!!!!!!!!!!!!!! TIME TO SCARE YOU!!!!!!!!!!!!!!! with: {WAV_FILES[randomWaveFile]}");
                _mediaPlayer?.Play();
                _led1PwmPin?.Start();
                _led2PwmPin?.Start();
                _led3PwmPin?.Start();
                _motorPin?.Start();

                //start up the servo to make it look like the skull is laughing
                _servotimer = ThreadPoolTimer.CreatePeriodicTimer(OnServoTick, TimeSpan.FromMilliseconds(200));
                _eyesLedTimer = ThreadPoolTimer.CreatePeriodicTimer(OnEyesLedTick, TimeSpan.FromMilliseconds(500));
                //_relaySwitchTimer = ThreadPoolTimer.CreatePeriodicTimer(OnRelaySwitching, TimeSpan.FromMilliseconds(250));

                await Task.Factory.StartNew(PulseLED, _greenLedPwmCancellationToken.Token);
                //start the eyes blinking

                //Run the LEDS pattern for the upside down cross on the skull head
                await Task.Run(RunLeds);
                //Start he LED in the mouth to pulse while audio is playing
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Playing the audio and or running LEDS through an exception: \r\n{ex.Message}");
                _greenLedPwmCancellationToken.Cancel();
                TurnOffLeds();
                _led1PwmPin.Stop();
                _led2PwmPin.Stop();
                _led3PwmPin.Stop();
                _motorPin.Stop();
                _turnOffLEDS = true;
                _servotimer.Cancel();
                _eyesLedTimer.Cancel();
                _relaySwitchTimer.Cancel();
            }
        }

        private async void OnDhtValuesChanged(object sender, DHTTempArgs e) => await Task.Run(() =>
        {
            _bluemixDeviceClient?.publishEvent("TempEvent", "json", JsonConvert.SerializeObject(e));
            Debug.WriteLine($"Temperature: {e.Temperature}");
        });

        private void OnRelaySwitching(ThreadPoolTimer timer)
        {
            //Open and close the relay every half second
            _relaySwitchPin.Write(_relaySwitchPin.Read() == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High);
        }

        private void OnWaveFilefinishedPlaying(MediaPlayer sender, object args)
        {
            Debug.WriteLine("Finished Audio!");
            _motorPin?.Stop();
            _servotimer?.Cancel();
            _eyesLedTimer?.Cancel();
            _relaySwitchTimer?.Cancel();
            _led1PwmPin?.Stop();
            _led2PwmPin?.Stop();
            _led3PwmPin?.Stop();
            TurnOffLeds();
            //Cancel the LED thread for the PWM
            _greenLedPwmCancellationToken.Cancel();
        }

        private async void OnEyesLedTick(ThreadPoolTimer timer)
        {
            _rightEyeLed.Write(_rightEyeLed.Read() == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High);
            await Task.Delay(100);
            _leftEyeLed.Write(_leftEyeLed.Read() == GpioPinValue.High ? GpioPinValue.Low : GpioPinValue.High);
        }

        private void TurnOffLeds()
        {
            try
            {
                _serialDigitalInputPin?.Write(GpioPinValue.Low);
                _registerClockPin?.Write(GpioPinValue.Low);
                _serialClockPin?.Write(GpioPinValue.Low);
                _leftEyeLed?.Write(GpioPinValue.Low);
                _rightEyeLed?.Write(GpioPinValue.Low);
                _led1PwmPin?.SetActiveDutyCyclePercentage(_restingPulseLegnth);
                _led2PwmPin?.SetActiveDutyCyclePercentage(_restingPulseLegnth);
                _led3PwmPin?.SetActiveDutyCyclePercentage(_restingPulseLegnth);
                ShifIn(0x00);
                PulseRegisterClock();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{nameof(TurnOffLeds)} threw this exception: {ex.Message}");
            }
            // initialize the pins to low

            _turnOffLEDS = true;
        }

        private async void PulseLED()
        {
            while (!_turnOffLEDS)
            {
                for (double x = 0; x < 1; x += .1)
                {
                    _led1PwmPin?.SetActiveDutyCyclePercentage(x);
                    _led2PwmPin?.SetActiveDutyCyclePercentage(x);
                    _led3PwmPin?.SetActiveDutyCyclePercentage(x);
                    await Task.Delay(30);
                }

                for (double x = 1; x > .0; x -= .1)
                {
                    _led1PwmPin?.SetActiveDutyCyclePercentage(x);
                    _led2PwmPin?.SetActiveDutyCyclePercentage(x);
                    _led3PwmPin?.SetActiveDutyCyclePercentage(x);
                    await Task.Delay(30);
                }
            }
            _led1PwmPin.SetActiveDutyCyclePercentage(_restingPulseLegnth);
            _led2PwmPin.SetActiveDutyCyclePercentage(_restingPulseLegnth);
            _led3PwmPin.SetActiveDutyCyclePercentage(_restingPulseLegnth);
        }

        private async Task RunLeds()
        {
            // initialize the pins to low
            _serialDigitalInputPin.Write(GpioPinValue.Low);
            _registerClockPin.Write(GpioPinValue.Low);
            _serialClockPin.Write(GpioPinValue.Low);

            // main loop
            while (!_turnOffLEDS)
            {
                //Run the LEDS from top to bottom of cross
                foreach (byte led in LED)
                {
                    ShifIn(led);
                    PulseRegisterClock();

                    await Task.Delay(50);
                }

                // Flash all the LED's on the cross 5 times
                for (int i = 0; i < 5; i++)
                {
                    ShifIn(0xff);
                    PulseRegisterClock();
                    await Task.Delay(100);
                    ShifIn(0x00);
                    PulseRegisterClock();
                    await Task.Delay(100);
                }
                await Task.Delay(500);

                //Flash the I part of cross than flash T part of cross on and off

                // Run the LEDS from bottom to top on the cross
                foreach (byte led in LED.Reverse())
                {
                    ShifIn(led);
                    PulseRegisterClock();

                    await Task.Delay(50);
                }
            }
            TurnOffLeds();
        }

        private void ShifIn(byte b)

        {
            //The basic routine for bit banging in series
            for (var i = 0; i < LED.Length; i++)
            {
                _serialDigitalInputPin.Write((b & (0x80 >> i)) > 0 ? GpioPinValue.High : GpioPinValue.Low);
                PulseSerialClock();
            }
        }

        private void PulseRegisterClock()
        {
            _registerClockPin.Write(GpioPinValue.Low);
            _registerClockPin.Write(GpioPinValue.High);
        }

        private void PulseSerialClock()
        {
            _serialClockPin.Write(GpioPinValue.Low);
            _serialClockPin.Write(GpioPinValue.High);
        }

        ~StartupTask()
        {
            _mcp3008.LightingValueChanged -= OnLightingValueChanged;
            _dhtSensor.DhtValuesChanged -= OnDhtValuesChanged;
            _mediaPlayer.MediaEnded -= OnWaveFilefinishedPlaying;
            _pirMotionSensor.MotionDetected -= OnMotionDetected;
            _greenLedPwmCancellationToken.Cancel();
            _eyesLedTimer?.Cancel();
            _pwmController = null;
            _gpioController = null;
            _led1PwmPin?.Stop();
            _led2PwmPin?.Stop();
            _led3PwmPin?.Stop();
            _motorPin?.Stop();
            _rightEyeLed?.Write(GpioPinValue.Low);
            _leftEyeLed?.Write(GpioPinValue.Low);
            _deferral?.Complete();
        }
    }
}