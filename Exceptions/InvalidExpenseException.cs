namespace ExpenseTracker.Exceptions;

/// <summary>
/// Thrown when an expense (or one of its fields) holds data that breaks a business rule,
/// for example an empty title, a negative amount or a date in the future.
/// </summary>
public class InvalidExpenseException : Exception
{
    public InvalidExpenseException()
        : base("The expense data is invalid.")
    {
    }

    public InvalidExpenseException(string message)
        : base(message)
    {
    }

    public InvalidExpenseException(string message, string fieldName)
        : base(message)
    {
        FieldName = fieldName;
    }

    public InvalidExpenseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The name of the field that failed validation, if known (e.g. "Amount").</summary>
    public string? FieldName { get; }
}
