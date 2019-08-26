//*****************************************************************************
// Author            : Yagil Ovadia
// File name         : Bot.cs
//*****************************************************************************

using System.Net;

namespace cNc
{
    internal class Bot
    {
        public int iBotListenPort;
        public IPEndPoint botIpEndpoint;
        public string szBotIpAddress;

        public Bot(int iBotListenPortIn, IPEndPoint botipEndpointIn)
        {
            iBotListenPort = iBotListenPortIn;
            botIpEndpoint = botipEndpointIn;
            szBotIpAddress = GetIpAddressStrFromIPEndPoint(botipEndpointIn);
        }

        public static string GetIpAddressStrFromIPEndPoint(IPEndPoint iPEndPoint)
        {
            return IPAddress.Parse(iPEndPoint.Address.ToString()).ToString();
        }

        public override bool Equals(object obj)
        {
            Bot otherBot = (Bot)obj;

            string szOtherBotIpAddress = GetIpAddressStrFromIPEndPoint(otherBot.botIpEndpoint);

            return szBotIpAddress.Equals(szOtherBotIpAddress) && iBotListenPort == otherBot.iBotListenPort;
        }
    }
}

/* End Of File */
