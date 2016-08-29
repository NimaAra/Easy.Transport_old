namespace EasyTransport.Common.Extensions
{
    using Easy.Common.Extensions;

    /// <summary>
    /// Provides a set of extensions for working with <see cref="byte"/>
    /// </summary>
    internal static class ByteExtensions
    {
        internal static bool IsPing(this byte[] data)
        {
            return data.Compare(Constants.OpCode_Ping);
        }

        internal static bool IsPong(this byte[] data)
        {
            return data.Compare(Constants.OpCode_Pong);
        }
    }
}