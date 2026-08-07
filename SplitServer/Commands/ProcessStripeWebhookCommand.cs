using CSharpFunctionalExtensions;
using MediatR;

namespace SplitServer.Commands;

public class ProcessStripeWebhookCommand : IRequest<Result>
{
    /// <summary>The request body exactly as received. Any re-serialisation would break the signature check.</summary>
    public required string Payload { get; init; }

    public required string? SignatureHeader { get; init; }
}
