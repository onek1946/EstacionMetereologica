using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using I2CBusCode;
using System.Threading;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;

namespace EstacionMeteorologica
{

    public abstract class Sensor 
    {
        protected I2CDevice.Configuration _slaveConfig; //referencia para el I2CDevice configuration
        protected const byte ClockRateKHz = 40; // clock rate para el bus I2C
        protected const int TransactionTimeout = 1000; //timeout para el bus I2C

        public virtual void TakeMeasurements() { }
        public virtual void GetCalibrationData() { }
        public virtual string GetDataString() { return ""; }
    }

    public class BMP085:Sensor
    {

        // These wait times correspond to the oversampling settings.  
        // Please see the datasheet for this sensor for more information.
        private readonly byte[] _pressureWaitTime = new byte[] { 5, 8, 14, 26 };
        //Variables con mediciones.
        private float _celsius;
        private int _pascal;
        private byte _address;
        // Variables para la calibracion
        private short _ac1;
        private short _ac2;
        private short _ac3;
        private ushort _ac4;
        private ushort _ac5;
        private ushort _ac6;
        private short _b1;
        private short _b2;
        private short _mb;
        private short _mc;
        private short _md;
        // Oversampling for measurements.  Please see the datasheet for this sensor for more information.
        private byte _oversamplingSetting;
        public enum DeviceMode
        {
            UltraLowPower = 0,
            Standard = 1,
            HighResolution = 2,
            UltraHighResolution = 3
        }

        public BMP085(byte address, DeviceMode deviceMode)
        {
            Address = address;
            _slaveConfig = new I2CDevice.Configuration(address, ClockRateKHz);
            _oversamplingSetting = (byte)deviceMode;

            // Get calibration data that will be used for future measurement taking.
            GetCalibrationData();

            // Take initial measurements.
            TakeMeasurements();

            // Take new measurements every 30 seconds.
            //modificacion mia, en vez de tomar las mediciones aca, hago al metodo publico y lo llamo yo
            //_sensorTimer = new Timer(TakeMeasurements, null, 200, 30000);
        }
        public override void TakeMeasurements()
        {
            try{

            long x1, x2, x3, b3, b4, b5, b6, b7, p;

            long ut = ReadUncompensatedTemperature();
            long up = ReadUncompensatedPressure();

            // calculate the compensated temperature
            x1 = (ut - _ac6) * _ac5 >> 15;
            x2 = (_mc << 11) / (x1 + _md);
            b5 = x1 + x2;
            _celsius = (float)((b5 + 8) >> 4) / 10;

            // calculate the compensated pressure
            b6 = b5 - 4000;
            x1 = (_b2 * (b6 * b6 >> 12)) >> 11;
            x2 = _ac2 * b6 >> 11;
            x3 = x1 + x2;
            switch (_oversamplingSetting)
            {
                case 0:
                    b3 = ((_ac1 * 4 + x3) + 2) >> 2;
                    break;
                case 1:
                    b3 = ((_ac1 * 4 + x3) + 2) >> 1;
                    break;
                case 2:
                    b3 = ((_ac1 * 4 + x3) + 2);
                    break;
                case 3:
                    b3 = ((_ac1 * 4 + x3) + 2) << 1;
                    break;
                default:
                    throw new Exception("Oversampling setting must be 0-3");
            }
            x1 = _ac3 * b6 >> 13;
            x2 = (_b1 * (b6 * b6 >> 12)) >> 16;
            x3 = ((x1 + x2) + 2) >> 2;
            b4 = (_ac4 * (x3 + 32768)) >> 15;
            b7 = (up - b3) * (50000 >> _oversamplingSetting);
            p = (b7 < 0x80000000 ? (b7 * 2) / b4 : (b7 / b4) * 2);
            x1 = (p >> 8) * (p >> 8);
            x1 = (x1 * 3038) >> 16;
            x2 = (-7357 * p) >> 16;
            _pascal = (int)(p + ((x1 + x2 + 3791) >> 4));
            }
            catch(Exception e)
            {
              //Error al leer/escribir en el bus I2C, por ahora se ignora
            }      
        }
        public override void GetCalibrationData()
        {
            _ac1 = ReadShort(0xAA);
            _ac2 = ReadShort(0xAC);
            _ac3 = ReadShort(0xAE);
            _ac4 = (ushort)ReadShort(0xB0);
            _ac5 = (ushort)ReadShort(0xB2);
            _ac6 = (ushort)ReadShort(0xB4);
            _b1 = ReadShort(0xB6);
            _b2 = ReadShort(0xB8);
            _mb = ReadShort(0xBA);
            _mc = ReadShort(0xBC);
            _md = ReadShort(0xBE);
        }
        public override string GetDataString()
        {

            string str_dato = "";
            str_dato += "BMP085 Pascales: " + this.Pascal + "\n";
            str_dato += "BMP085 PulgadasMercurio: " + this.InchesMercury.ToString("F2") + "\n";
            str_dato += "BMP085 Temp*C: " + this.Celsius.ToString("F2") + "\n";
            return str_dato; 
        
        }
        private long ReadUncompensatedTemperature()
        {
            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[2] { 0xF4, 0x2E }, TransactionTimeout);

            // Required as per datasheet.
            Thread.Sleep(5);

            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[] { 0xF6 }, TransactionTimeout);

            // get MSB and LSB result
            byte[] inputData = new byte[2];
            I2CBus.GetInstance().Read(_slaveConfig, inputData, TransactionTimeout);

            return ((inputData[0] << 8) | inputData[1]);
        }
        private long ReadUncompensatedPressure()
        {
            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[2] { 0xF4, (byte)(0x34 + (_oversamplingSetting << 6)) }, TransactionTimeout);

            // insert pressure waittime using oversampling setting as index.
            Thread.Sleep(_pressureWaitTime[_oversamplingSetting]);

            // get MSB and LSB result
            byte[] inputData = new byte[3];
            I2CBus.GetInstance().ReadRegister(_slaveConfig, 0xF6, inputData, TransactionTimeout);

            return ((inputData[0] << 16) | (inputData[1] << 8) | (inputData[2])) >> (8 - _oversamplingSetting);
        }
        protected short ReadShort(byte registerAddress)
        {
            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[] { registerAddress }, TransactionTimeout);

            // get MSB and LSB result
            byte[] inputData = new byte[2];
            I2CBus.GetInstance().Read(_slaveConfig, inputData, TransactionTimeout);

            return (short)((inputData[0] << 8) | inputData[1]);
        }

        // Getters/setters
        public byte Address
        {
            get { return _address; }
            private set { _address = value; }
        }
        public int Pascal
        {
            get { return _pascal; }
        }
        public float InchesMercury
        {
            get
            {
                return (float)(_pascal / 3386.389);
            }
        }
        public float Celsius
        {
            get { return _celsius; }
        }
    }

    public class HH10D:Sensor
    {
        //Pin de frecuencia de entrada
        private InputPort _f_input;
        //Variable que indica la division de frecuencia externa
        private uint _divisor;
        //Variables para la calibracion
        private short _sensitivity;
        private short _offset;
        private byte _address;
        //Variable con la medicion
        private double _rel_humidity;

        public HH10D(byte address, Cpu.Pin in_pin,uint divisor)
        
        {
            this._divisor = divisor;
            Address = address;
            _slaveConfig = new I2CDevice.Configuration(address, ClockRateKHz);
            _f_input = new InputPort(in_pin, false, Port.ResistorMode.PullUp);
            GetCalibrationData();
            TakeMeasurements();
        }
        public override void GetCalibrationData()
        {
            _sensitivity = ReadShort(0x0A); Thread.Sleep(1); // 2 bytes de sensitivity (395)
            _offset = ReadShort(0x0C); Thread.Sleep(1); //2 bytes de offset (7620)
        }
        public override void TakeMeasurements()
        {
            bool ant, ahora;
             long init_time=0, fin_time=0;

            for (int i = 0; i < 3; i++)
            {
                ant = ahora = _f_input.Read();

                while (ant == ahora)
                {
                    ant = ahora;
                    ahora = _f_input.Read();
                }
                if(i==0)
                     init_time = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
                if(i==2)
                    fin_time =  Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
            }
                const Int64 ticks_per_millisecond = System.TimeSpan.TicksPerMillisecond;          

           
            double freq = (double)(ticks_per_millisecond*1000)/(double)(fin_time - init_time) ; 
            
            freq *= _divisor;

         //   bool holaaa = _f_input.Read();
            _rel_humidity = (double)((_offset-freq)* _sensitivity) / 4096.0; 
        
            
        }
        public override string GetDataString()
        {

            string str_dato = "";
            str_dato += "HH10D Humedad Relativa: " + this.Rel_Humidity.ToString("F2") + "\n";
            return str_dato;

        }
        protected short ReadShort(byte registerAddress)
        {
            // write register address
            I2CBus.GetInstance().Write(_slaveConfig, new byte[] { registerAddress }, TransactionTimeout);

            // get MSB and LSB result
            byte[] inputData = new byte[2];
            I2CBus.GetInstance().Read(_slaveConfig, inputData, TransactionTimeout);

            return (short)((inputData[0] << 8) | inputData[1]);
        }
        // Getters/setters
        public double Rel_Humidity 
        {
            get { return _rel_humidity; }
        }
        public byte Address
        {
            get { return _address; }
            private set { _address = value; }
        }

    }

    public class TLS230RLF : Sensor 
    {
        InputPort _p_input;
        OutputPort _o_output;
        char stateS;
        uint _divisor;
        double _luz;
        public TLS230RLF(Cpu.Pin in_pin, Cpu.Pin out_pin, uint divisor)
        {
            this._divisor = divisor;
            _p_input = new InputPort(in_pin, false, Port.ResistorMode.PullUp);
            _o_output = new OutputPort(out_pin, true);
            TakeMeasurements();
        }
        public override void GetCalibrationData()
        {
            _o_output.Write(true);
        }
        public override void TakeMeasurements()
        {
            bool ant, ahora;
            long init_time = 0, fin_time = 0;
            for (int i = 0; i < 3; i++)
            {
                ant = ahora = _p_input.Read();

                while (ant == ahora)
                {
                    ant = ahora;
                    ahora = _p_input.Read();
                }
                if (i == 0)
                    init_time = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
                if (i == 2)
                    fin_time = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
            }
            const Int64 ticks_per_millisecond = System.TimeSpan.TicksPerMillisecond;
            double freq = (double)(ticks_per_millisecond * 1000) / (double)(fin_time - init_time);
            freq *= _divisor;
            if (freq >= 50000 && _o_output.Read()==false)
            {
                _o_output.Write(true);
            }
            if (freq <= 512 && _o_output.Read()==true)
            {
                _o_output.Write(false);
            }
            if (_o_output.Read())
                _luz = (double)((freq*130) / 1000);
            else
                _luz = (double)((freq * 1.30) / 1000);
            //_luz = freq;
            _luz *= (double)(100.0/1000000.0);
            _luz *= 683.0;
        }
        public override string GetDataString()
        {

            string str_dato = "";
            str_dato += "TLS230RLF (Lumenes): " + this._luz.ToString("F2") + "\n";
            return str_dato;

        }

    }

    }