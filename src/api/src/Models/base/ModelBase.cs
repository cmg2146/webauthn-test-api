namespace WebAuthnTest.Api;

using System;

public class ModelBase
{
    public long Id { get; set; }
    public DateTime Created { get ; set; } = DateTime.UtcNow;
    public DateTime? Updated { get ; set; } = DateTime.UtcNow;
}