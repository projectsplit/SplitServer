using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class ProcessPlayNotificationCommand : IRequest<Result>
{
    /// <summary>The Pub/Sub push body exactly as received.</summary>
    public required string Payload { get; init; }

    /// <summary>The shared secret from the push URL's query string. Absent means refused.</summary>
    public required string? Secret { get; init; }
}
