namespace WebAuthnTest.Database;

using System;

public class Entity
{
    public long Id { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Updated { get; set; }
}
