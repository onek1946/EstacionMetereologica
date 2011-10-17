using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using SecretLabs.NETMF.Hardware;
using SecretLabs.NETMF.Hardware.NetduinoPlus;

namespace EstacionMeteorologica
{
    public class Programa
    {
        public static void Main()
        {

            //Creo el vector de sensores:
            Sensor[] Sensores = {
                                    new BMP085(0x77, BMP085.DeviceMode.UltraLowPower),
                                    new HH10D(0x51, Pins.GPIO_PIN_D1,256),
                                    new TLS230RLF(Pins.GPIO_PIN_D0,Pins.GPIO_PIN_D2,256)
            };

            //Configuro El endpoint remoto y el socket
            //La direccion del server es 192.168.1.40, el puerto es 9050 y se usa el protocolo TCP para la capa 4.
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("192.168.1.40"), 9050);
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(remoteEP);

            //Variables:
            string str_dato;

            //Loop Infinito
            while (true)
            {
                str_dato = "\n";

                //Muestreo datos de los sensores
                foreach (Sensor s in Sensores)
                        s.TakeMeasurements(); 

                //Cargo el string de datos:
                foreach (Sensor s in Sensores)
                    str_dato += s.GetDataString();

                //Intento Transmitir datos:
                try
                {
                    SocketUtility.Send(socket, Encoding.UTF8.GetBytes(str_dato), 0, str_dato.Length, 10000);
                }
                catch(SocketException ex)
                {
                    socket.Close();
                    throw ex;        //Por ahora no hago nada, solo paro el programa.
                }
                
                //Delay 500 ms
                Thread.Sleep(500);
            }
        }
    }

    public class SocketUtility
    {
        public static void Send(Socket socket, byte[] buffer, int offset, int size, int timeout)
        {
            //Ticks por ms, distinto al framework 4
            const Int64 ticks_per_millisecond = System.TimeSpan.TicksPerMillisecond;
            long startTickCount = Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks;
            int sent = 0;  // how many bytes is already sent
            do
            {
                if (Microsoft.SPOT.Hardware.Utility.GetMachineTime().Ticks > startTickCount + timeout * ticks_per_millisecond)
                    throw new Exception("Timeout viteh.");
                try
                {
                    sent += socket.Send(buffer, offset + sent, size - sent, SocketFlags.None);
                }
                catch (SocketException ex)
                {
                    /*       if (ex.SocketErrorCode == SocketError.WouldBlock ||
                                 ex.SocketErrorCode == SocketError.IOPending ||
                                 ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                             {
                                 // socket buffer probablemente lleno
                                 Thread.Sleep(30);
                             }
                             else
         
                     */
                    throw ex;  // no hay en ex campos q sean del tipo Socketerror
                }
            } while (sent < size);
        }
    }
}
