using SplitServer.Models;

namespace SplitServer.Dto;

public class CreateExpenseRequest
{
    public required string GroupId { get; init; }
    
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string Description { get; init; }

    public DateTime? Occured { get; init; }

    public required List<Payment> Payments { get; init; }

    public required List<Share> Shares { get; init; }

    public required List<string> Labels { get; init; }
    
    public required Location? Location { get; init; }
}