namespace SplitServer.Repositories;

/// <summary>
/// Narrows a user search to a set of ids, or away from one.
/// <para>
/// Listing connections before everyone else means running the same search twice over two disjoint
/// halves of the collection. Expressing that as a scope on the one search keeps both halves on
/// identical matching rules — a keyword that finds a name in one half cannot miss it in the other
/// because the two were written as separate queries that drifted apart.
/// </para>
/// </summary>
public record UserIdScope
{
    private UserIdScope(UserIdScopeKind kind, IList<string> ids)
    {
        Kind = kind;
        Ids = ids;
    }

    public UserIdScopeKind Kind { get; }

    public IList<string> Ids { get; }

    public static UserIdScope All { get; } = new(UserIdScopeKind.All, []);

    public static UserIdScope Only(IList<string> ids) => new(UserIdScopeKind.Only, ids);

    public static UserIdScope Except(IList<string> ids) => new(UserIdScopeKind.Except, ids);
}

public enum UserIdScopeKind
{
    All,
    Only,
    Except
}
