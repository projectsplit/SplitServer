﻿using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetLabelsQuery : IRequest<Result<GetLabelsResponse>>
{
    public required string UserId { get; init; }
    public required string GroupId { get; init; }
}