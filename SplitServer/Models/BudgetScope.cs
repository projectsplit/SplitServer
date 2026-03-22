namespace SplitServer.Models;

[Flags]
public enum BudgetScope
{
    None = 0,
    Personal = 1,
    NonGroup = 2,
    Group = 4
}