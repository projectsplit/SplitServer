using CSharpFunctionalExtensions;
using MediatR;
using SplitServer.Repositories;
using SplitServer.Responses;

namespace SplitServer.Queries;

public class GetExpenseTimeBucketsQueryHandler : IRequestHandler<GetExpenseTimeBucketsQuery, Result<GetExpenseTimeBucketsResponse>>
{
    private readonly IUsersRepository _usersRepository;
    private readonly IGroupsRepository _groupsRepository;
    private readonly IExpensesRepository _expensesRepository;

    public GetExpenseTimeBucketsQueryHandler(
        IUsersRepository usersRepository,
        IGroupsRepository groupsRepository,
        IExpensesRepository expensesRepository)
    {
        _usersRepository = usersRepository;
        _groupsRepository = groupsRepository;
        _expensesRepository = expensesRepository;
    }

    public async Task<Result<GetExpenseTimeBucketsResponse>> Handle(GetExpenseTimeBucketsQuery query, CancellationToken ct)
    {
        if (query.StartDate >= query.EndDate)
        {
            return Result.Failure<GetExpenseTimeBucketsResponse>("Start date must be before end date");
        }

        if (query.BucketDurationInSeconds <= 0)
        {
            return Result.Failure<GetExpenseTimeBucketsResponse>("Bucket duration must be greater than 0");
        }

        var bucketDuration = TimeSpan.FromSeconds(query.BucketDurationInSeconds);

        var bucketCount = (query.EndDate - query.StartDate) / bucketDuration;

        if (bucketCount % 1 != 0)
        {
            return Result.Failure<GetExpenseTimeBucketsResponse>("Total duration is not a multiple of the bucket duration");
        }

        var userMaybe = await _usersRepository.GetById(query.UserId, ct);

        if (userMaybe.HasNoValue)
        {
            return Result.Failure<GetExpenseTimeBucketsResponse>($"User with id {query.UserId} was not found");
        }

        var groups = await _groupsRepository.GetAllByUserId(query.UserId, ct);
        var membersByGroup = groups.ToDictionary(x => x.Id, x => x.Members.First(m => m.UserId == query.UserId));
        var memberIds = membersByGroup.Select(m => m.Value.Id).ToList();

        var expenses = await _expensesRepository.GetAllByMemberIds(memberIds, query.StartDate, query.EndDate, ct);

        var timeBuckets = Enumerable.Range(0, (int)bucketCount)
            .Select(
                x => new TimeBucket
                {
                    StartDate = query.StartDate + x * bucketDuration,
                    EndDate = query.StartDate + x * bucketDuration + bucketDuration,
                    Amount = 0
                })
            .ToList();

        var cumulativeSum = 0m;

        foreach (var bucket in timeBuckets)
        {
            var expensesInBucket = expenses.Where(x => x.Occurred >= bucket.StartDate && x.Occurred < bucket.EndDate);
            var bucketSum = expensesInBucket.Sum(x => x.Amount);
            cumulativeSum += bucketSum;
            bucket.Amount = cumulativeSum;
        }

        return new GetExpenseTimeBucketsResponse
        {
            Buckets = timeBuckets
        };
    }
}

public record TimeBucket
{
    public required DateTime StartDate { get; set; }
    public required DateTime EndDate { get; set; }
    public decimal Amount { get; set; }
}