﻿using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetGroupsWithDetailsQuery : IRequest<Result<GetGroupsWithDetailsResponse>>
{
    public required string UserId { get; init; }
    public required bool? IsArchived { get; init; }
    public required int PageSize { get; init; }
    public required string? Next { get; init; }
}