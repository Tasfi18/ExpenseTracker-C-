namespace ExpenseTracker.Models;

/// <summary>The orders in which the expense list can be shown.</summary>
public enum SortOption
{
    DateNewestFirst,
    DateOldestFirst,
    AmountHighestFirst,
    AmountLowestFirst,
    Title,
    Category,
}
