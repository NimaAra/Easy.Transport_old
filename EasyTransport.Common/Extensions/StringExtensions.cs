namespace EasyTransport.Common.Extensions
{
    using System.Diagnostics;

    /// <summary>
    /// Extensions for <see cref="string"/>
    /// </summary>
    internal static class StringExtensions
    {
        /// <summary>
        /// A nicer way of calling <see cref="string.IsNullOrEmpty(string)"/>
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns><see langword="true"/> if the format parameter is null or an empty string (""); otherwise, <see langword="false"/>.</returns>
        [DebuggerStepThrough]
        internal static bool IsNullOrEmpty(this string value)
        {
            return string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// A nicer way of calling the inverse of <see cref="string.IsNullOrEmpty(string)"/>
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns><see langword="true"/> if the format parameter is not null or an empty string (""); otherwise, <see langword="false"/>.</returns>
        [DebuggerStepThrough]
        internal static bool IsNotNullOrEmpty(this string value)
        {
            return !value.IsNullOrEmpty();
        }

        /// <summary>
        /// A nice way of checking if a string is null, empty or whitespace 
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns><see langword="true"/> if the format parameter is null or an empty string (""); otherwise, <see langword="false"/>.</returns>
        [DebuggerStepThrough]
        internal static bool IsNullOrEmptyOrWhiteSpace(this string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// A nice way of checking the inverse of (if a string is null, empty or whitespace) 
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns><see langword="true"/> if the format parameter is not null or an empty string (""); otherwise, <see langword="false"/>.</returns>
        [DebuggerStepThrough]
        internal static bool IsNotNullOrEmptyOrWhiteSpace(this string value)
        {
            return !value.IsNullOrEmptyOrWhiteSpace();
        }
    }

}