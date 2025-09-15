/*
 * Minimal Validate helper for Jobber configuration validation.
 * This duplicates a small subset of validation helpers used by Jobber.
 */
using System;

namespace Jobber.Config
{

    internal static class Validate
    {
        public static void EnsureRange(int value, int min, int max, string message)
        {
            if (value < min || value > max)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void EnsureNotNull(string? s, string message)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}