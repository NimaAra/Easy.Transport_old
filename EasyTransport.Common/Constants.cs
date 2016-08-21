namespace EasyTransport.Common
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text;

    /// <summary>
    /// Stores constants used throughout the library.
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class Constants
    {
        internal static string ClientRequestedIdKey = "client-req-id312670ec-0892-4b88-9d8e-5771f2a75c47";
        internal static byte[] OpCode_Ping = Encoding.ASCII.GetBytes("9");
        internal static byte[] OpCode_Pong = Encoding.ASCII.GetBytes("10");
    }
}