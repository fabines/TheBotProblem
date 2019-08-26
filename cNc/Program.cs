namespace cNc
{
    class Program
    {

        static void Main(string[] args)
        {
            CNCServer cNcServer = new CNCServer(31337, "Moses", "⟵ 𝕸𝖔𝖘𝖊𝖘 ⟶");
            cNcServer.Start();
        }
    }
}
