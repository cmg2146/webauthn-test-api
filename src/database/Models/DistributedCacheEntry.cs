namespace WebAuthnTest.Database;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-6.0#distributed-sql-server-cache
[Table(DistributedCacheEntryConstants.TableName, Schema = DistributedCacheEntryConstants.SchemaName)]
public class DistributedCacheEntry
{
    [Key]
    [Required]
    [MaxLength(900)]
    public string Id { get; set; } = default!;
    [Required]
    public byte[] Value { get; set; } = default!;
    [Required]
    public DateTimeOffset ExpiresAtTime { get; set; }
    public long? SlidingExpirationInSeconds { get; set; }
    public DateTimeOffset? AbsoluteExpiration { get; set; }
}

public static class DistributedCacheEntryConstants
{
    public const string TableName = "DistributedCacheEntry";
    public const string SchemaName = "dbo";
}
