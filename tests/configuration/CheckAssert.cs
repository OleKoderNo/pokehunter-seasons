using System;
using System.IO;

/// <summary>
/// Shared assertions used by the standalone configuration checks.
/// </summary>
internal static class CheckAssert
{
    /// <summary>
    /// Requires an operation to throw InvalidDataException with all
    /// supplied message fragments.
    /// </summary>
    /// <param name="operation">
    /// The loader call or other operation being checked.
    /// </param>
    /// <param name="expectedMessageParts">
    /// Message fragments identifying the expected rejection reason.
    /// Matching is case-insensitive.
    /// </param>
    public static void Rejected(
        Action operation,
        params string[] expectedMessageParts
    )
    {
        try
        {
            operation();
        }
        catch (InvalidDataException exception)
        {
            foreach (string expectedPart in expectedMessageParts)
            {
                if (exception.Message.IndexOf(
                    expectedPart,
                    StringComparison.OrdinalIgnoreCase
                ) < 0)
                {
                    throw new InvalidOperationException(
                        "Configuration was rejected for an unexpected reason. " +
                        "Expected the message to contain '" +
                        expectedPart + "'. Actual message: " +
                        exception.Message,
                        exception
                    );
                }
            }

            // The operation rejected its input for the expected reason.
            return;
        }

        // Other exception types propagate automatically.
        // Reaching here means no exception was thrown at all.
        throw new InvalidOperationException(
            "Invalid configuration was accepted."
        );
    }
}