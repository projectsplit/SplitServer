using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Models;
using SplitServer.Repositories;
using SplitServer.Responses;
using SplitServer.Services;

namespace SplitServer.Queries;

public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, Result<GetNotificationsResponse>>
{
    private readonly INotificationsRepository _notificationsRepository;

    public GetNotificationsQueryHandler(INotificationsRepository notificationsRepository)
    {
        _notificationsRepository = notificationsRepository;
    }

    public async Task<Result<GetNotificationsResponse>> Handle(GetNotificationsQuery query, CancellationToken ct)
    {
        var nextDetails = Next.Parse<NotificationsNext>(query.Next);
        var maxCreatedDate = nextDetails?.MaxCreatedDate ?? DateTime.UtcNow;

        var notifications = await _notificationsRepository.GetByUserId(query.UserId, query.PageSize, maxCreatedDate, ct);

        var responseItems = notifications
            .Select(x => new NotificationResponseItem
            {
                Id = x.Id,
                Created = x.Created,
                Title = x.Title,
                Body = x.Body,
                Url = x.Url,
            })
            .ToList();

        return new GetNotificationsResponse
        {
            Notifications = responseItems,
            Next = CreateNext(query.PageSize, notifications)
        };
    }

    private static string? CreateNext(int pageSize, List<Notification> notifications)
    {
        return Next.Create(notifications, pageSize, x => new NotificationsNext { MaxCreatedDate = x.Last().Created });
    }
}

file class NotificationsNext
{
    public required DateTime MaxCreatedDate { get; init; }
}
