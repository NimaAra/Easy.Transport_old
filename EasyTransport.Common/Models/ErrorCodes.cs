namespace EasyTransport.Common.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Contains web-socket related error codes and their descriptions.
    /// </summary>
    internal static class ErrorCodes
    {
        /// <summary>
        /// Gets the web-socket related error codes and their descriptions.
        /// </summary>
        internal static readonly Dictionary<short, string> StatusCodeToReasonMap = new Dictionary<short, string>
        {
            {1000, "Normal"},
            {1001, "Away"},
            {1002, "ProtocolError"},
            {1003, "UnsupportedData"},
            {1004, "Undefined"},
            {1005, "NoStatus"},
            {1006, "Abnormal"},
            {1007, "InvalidData"},
            {1008, "PolicyViolation"},
            {1009, "TooBig"},
            {1010, "MandatoryExtension"},
            {1011, "ServerError"},
            {1015, "TLSHandshakeFailure"},
            {10061, "ConnectionRefused"}
        };
    }
}