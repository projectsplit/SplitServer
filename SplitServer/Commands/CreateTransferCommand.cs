using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class CreateTransferCommand : IRequest<Result<CreateTransferResponse>>
{
    public string UserId { get; }
    public string GroupId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public string Description { get; }
    public DateTime? Occured { get; }
    public string SenderId { get; }
    public string ReceiverId { get; }

    public CreateTransferCommand(
        string userId,
        string groupId,
        decimal amount,
        string currency,
        string description,
        DateTime? occured,
        string senderId,
        string receiverId)
    {
        UserId = userId;
        GroupId = groupId;
        Amount = amount;
        Currency = currency;
        Description = description;
        Occured = occured;
        SenderId = senderId;
        ReceiverId = receiverId;
    }
}