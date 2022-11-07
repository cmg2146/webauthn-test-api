namespace WebAuthnTest.Database;

using System;

public class Entity
{
    public long Id { get; set; }
    public DateTime Created { get ; set; } = DateTime.MinValue;
    public DateTime? Updated { get ; set; }
}