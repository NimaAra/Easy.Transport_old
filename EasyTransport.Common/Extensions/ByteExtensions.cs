namespace EasyTransport.Common.Extensions
{
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Provides a set of extensions for working with <see cref="byte"/>
    /// </summary>
    internal static class ByteExtensions
    {
        internal static bool IsPing(this byte[] data)
        {
            return ByteArrayCompare(data, Constants.OpCode_Ping);
        }

        internal static bool IsPong(this byte[] data)
        {
            return ByteArrayCompare(data, Constants.OpCode_Pong);
        }

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, long count);

        private static bool ByteArrayCompare(byte[] b1, byte[] b2)
        {
            // Validate buffers are the same length.
            // This also ensures that the count does not exceed the length of either buffer.  
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }
    }
}