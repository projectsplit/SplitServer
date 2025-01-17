using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Dto;

namespace SplitServer.Commands;

public class CreateGroupCommand : IRequest<Result<CreateGroupResponse>>
{
    public string UserId { get; }
    
    public string Name { get; }
    
    public string Currency { get; } 

    public CreateGroupCommand(
        string userId,
        string name,
        string currency)
    {
        UserId = userId;
        Name = name;
        Currency = currency;
    }
}