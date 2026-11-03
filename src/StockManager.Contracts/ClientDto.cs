namespace StockManager.Contracts;

/// <summary>
/// One client returned by <c>GET /api/clients</c>. <see cref="Code"/> is the stable reference to the client
/// and does not change when <see cref="Name"/> changes.
/// </summary>
public sealed record ClientDto(int Id, string Code, string Name);
