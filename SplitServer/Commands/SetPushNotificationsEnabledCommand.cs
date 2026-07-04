using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class SetPushNotificationsEnabledCommand : IRequest<Result>
{
    public required string UserId { get; init; }
    public required bool Enabled { get; init; }
}
