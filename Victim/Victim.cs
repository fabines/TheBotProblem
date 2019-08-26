//*****************************************************************************
// Author            : Yagil Ovadia
// File name         : Victim.cs
//*****************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Victim
{
    internal class VictimClient
    {
        private Timer _timer;
        private Socket _serverSocket;
        private string _victimPassword;
        private int _iCurrentTcpListenPort;
        private byte[] _bufferBytes = new byte[256];
        private Dictionary<Socket, string> _socketToClientsMessageDict;
        private string _matchPasswordPattern = "([a-z]){6}";

        #region Constatns
        private const string szConnectionListenMessage = "Server listening on port ";
        private const string szClientHackedByPrefixMessage = "Hacked by";
        private const string szChosenPasswordMessage = ", password is ";
        private readonly string szPasswordRequestMessage = "Please enter your password" + Environment.NewLine;
        private readonly string szCorrectPasswordMessage = "Access granted" + Environment.NewLine;
        private const string szRequestVictimPasswordMessage = "Please enter your password: (must be 6 character length [a-z]) \n> ";
        private const string szConsoleTitle = "⋐⋐⋐⋐⋐ 𝓥𝒾𝒸𝓉𝒾𝓂 ⋑⋑⋑⋑⋑";
        private static object MessageLock = new object(); /* Used for coloring the text */
        #endregion

        public VictimClient()
        {
            /* Set the console title */
            Console.Title = szConsoleTitle;

            /* Incoming port connection */
            _iCurrentTcpListenPort = 0;

            /* Create a socket for the server */
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            /* Animated Console Title */
            Thread animatedConsoleTitle = new Thread(AnimateConsoleTitle) { Name = "animatedConsoleTitleThread" };
            animatedConsoleTitle.Start();

            /* Gets a new TCP available port for listening */
            _iCurrentTcpListenPort = GetAvailablePortForListening();

            /* Create a random password with length n */
            _victimPassword = CreatePassword(6);

            /* Print listening message to screen */
            ConsoleWriteColoredMessage(GetClientStartMessage(), ConsoleColor.Green);

            /* Used for authenticating the client */
            HandleAuthClientsWithTimer();

            /* Listen for incoming TCP connections */
            StartAcceptingClients();
        }

        private void ResetStorageDataStructure()
        {
            /* Create data structure for handling clients */
            _socketToClientsMessageDict = new Dictionary<Socket, string>();

            /* Clear the dictionary before starting */
            _socketToClientsMessageDict.Clear();
        }

        private string GetClientStartMessage()
        {
            string szCurrentTcpListenPort = _iCurrentTcpListenPort.ToString();
            return szConnectionListenMessage + szCurrentTcpListenPort + szChosenPasswordMessage + _victimPassword;
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
                Thread.Sleep(5000);
                szPartialTitle = "";
            }
        }
        private static int GetAvailablePortForListening()
        {
            /* Dynamic ports - private ports range */
            const int iPortStartIndex = 49152;
            const int iPortEndIndex = 65535;

            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpEndPoints = properties.GetActiveTcpListeners();

            List<int> usedPorts = tcpEndPoints.Select(p => p.Port).ToList();
            int iUnusedPortIndex = 0;

            for (int iPort = iPortStartIndex; iPort < iPortEndIndex; iPort++)
            {
                if (usedPorts.Contains(iPort))
                {
                    continue;
                }
                iUnusedPortIndex = iPort;
                break;
            }
            return iUnusedPortIndex;
        }
        private void SetVicimPasswordFromConsole()
        {
            bool isPasswordSet = false;
            while (!isPasswordSet)
            {
                Console.Write(szRequestVictimPasswordMessage);
                string userEnteredPassword = Console.ReadLine();

                if (null != userEnteredPassword && Regex.IsMatch(userEnteredPassword, _matchPasswordPattern))
                {
                    _victimPassword = userEnteredPassword;
                }
            }
        }
        public string CreatePassword(uint length)
        {
            const string valid = "abcdefghijklmnopqrstuvwxyz";
            StringBuilder res = new StringBuilder();
            Random rnd = new Random();
            while (0 < length--)
            {
                res.Append(valid[rnd.Next(valid.Length)]);
            }
            return res.ToString();
        }
        public void ConsoleWriteColoredMessage(string message, ConsoleColor color)
        {
            lock (MessageLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
        }

        private void StartAcceptingClients()
        {
            /* Reset Dictionary */
            ResetStorageDataStructure();

            /* Assosiate a socket to a local port */
            try
            {
                _serverSocket.Bind(new IPEndPoint(IPAddress.Any, _iCurrentTcpListenPort));
            }
            catch (Exception)
            {
                Console.WriteLine("Cannot bind to local port, please try again");
                Console.ReadKey();
                StartAcceptingClients();
            }

            /* Sets the queue length of clients */
            _serverSocket.Listen(int.MaxValue);
        }

        private void DisconnectClient(Socket socket)
        {
            if (null != socket)
            {
                try
                {
                    /* Declare end of the connection */
                    socket.Shutdown(SocketShutdown.Both);

                    /* Free up any memory associated with the socket.*/
                    socket.Close();
                }
                catch (Exception)
                {

                }
                if (_socketToClientsMessageDict.ContainsKey(socket))
                {
                    /* Deletes the client from the data structure */
                    _socketToClientsMessageDict.Remove(socket);
                }
            }
        }

        private void DisconnectAll()
        {
            /* Disconnect all the clients in the data stracture */
            foreach (Socket socket in new List<Socket>(_socketToClientsMessageDict.Keys))
            {
                DisconnectClient(socket);
            }

            /* After disconeection, reset the data stracture to accept fresh clients */
            ResetStorageDataStructure();
        }
        private void HandleAuthClientsWithTimer(int iSecondsInterval = 40)
        {
            /* Start the timer thread with the given iterval */
            _timer = new Timer(e => HandleAuthenticatedClients(), null, TimeSpan.Zero, TimeSpan.FromSeconds(iSecondsInterval));
        }

        private void HandleAuthenticatedClients()
        {
            Dictionary<Socket, string> clientSocketDictionary = new Dictionary<Socket, string>();
            Dictionary<Socket, string> copiedClientSocketDictionary = new Dictionary<Socket, string>(_socketToClientsMessageDict);

            /* Print the client's messages and only those are authenticated -> their message is not null */
            foreach (KeyValuePair<Socket, string> entry in copiedClientSocketDictionary)
            {
                string szClientHackedMessage = entry.Value;
                if (null != entry.Key && null != szClientHackedMessage)
                {
                    clientSocketDictionary.Add(entry.Key, entry.Value);
                }
            }

            if (10 <= clientSocketDictionary.Count)
            {
                foreach (KeyValuePair<Socket, string> entry in clientSocketDictionary)
                {
                    string szClientHackedMessage = entry.Value;
                    ConsoleWriteColoredMessage(szClientHackedMessage, ConsoleColor.White);
                }
            }
            /* Finally disconecct all the clients - Wait for the new wave */
            DisconnectAll();

            //Start();
        }

        public void Start()
        {
            /* Start accepting clients */
            _serverSocket.BeginAccept(AcceptCallback, null);
        }
        private void AcceptCallback(IAsyncResult arAsyncResult)
        {
            /* Gets client's socket */
            Socket clientSocket;

            try
            {
                clientSocket = _serverSocket.EndAccept(arAsyncResult);
            }
            catch (ObjectDisposedException) /* Error accepting client */
            {
                return;
            }
            /* Client not in the data stracture */
            if (clientSocket != null && !_socketToClientsMessageDict.ContainsKey(clientSocket))
            {
                IPAddress remoteAddress = IPAddress.Parse(((IPEndPoint)clientSocket.RemoteEndPoint).Address.ToString());

                /* Gets the client ip address */
                string szClientIpAddressStr = remoteAddress.ToString();
            }

            /* Accept a new client - Async */
            Start();

            try
            {
                /* Handle client's credentials */
                AuthenticateClient(clientSocket);
            }
            catch (Exception) /* Error accepting client */
            {
                DisconnectClient(clientSocket);
            }

            /* Accept a new client - Async */
            _serverSocket.BeginAccept(AcceptCallback, null);
        }

        private void AuthenticateClient(Socket clientSocket)
        {
            /* Write password request message to the socket */
            SendDataToSocket(clientSocket, szPasswordRequestMessage);

            /* Get the password from the client */
            string szClientPassword = GetDataFromSocket(clientSocket, 6);

            /* Update the password to be without newline char */
            szClientPassword = szClientPassword?.Replace(Environment.NewLine, "");

            /* Validate login */
            if (_victimPassword != szClientPassword)
            {
                DisconnectClient(clientSocket);
            }
            else
            {
                /* Send the authentication message */
                SendDataToSocket(clientSocket, szCorrectPasswordMessage);

                /* Client was authenticated - add to data stracture */
                if (!_socketToClientsMessageDict.ContainsKey(clientSocket))
                {
                    /* Adds a new entry for connected client */
                    _socketToClientsMessageDict[clientSocket] = null;
                }

                /* Get client's message */
                string szClientHackedMessage = GetDataFromSocket(clientSocket);

                /* Update the password to be without newline char */
                szClientHackedMessage = szClientHackedMessage?.Replace(Environment.NewLine, "");

                /* Validate hacked message */
                if (szClientHackedMessage.StartsWith(szClientHackedByPrefixMessage))
                {
                    /* Adds the client message to the dictionary */
                    _socketToClientsMessageDict[clientSocket] = szClientHackedMessage;
                }
            }
        }

        private string GetDataFromSocket(Socket client, int iBytes = 128)
        {
            byte[] bytesInput = new byte[iBytes];
            int iRecvIncome = client.Receive(bytesInput);
            byte[] byteBuffer = new byte[iRecvIncome];
            Array.Copy(bytesInput, byteBuffer, iRecvIncome);
            return Encoding.ASCII.GetString(byteBuffer);
        }
        private void SendDataToSocket(Socket socket, string szInputData)
        {
            byte[] msg = Encoding.ASCII.GetBytes(szInputData);
            socket.Send(msg, 0, msg.Length, SocketFlags.None);
        }
    }
}

/* End Of File */
