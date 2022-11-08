namespace WebAuthnTest.Api;

using System;

public class ModelBase
{
    public long Id { get; set; }
    public DateTime Created { get ; set; } = DateTime.MinValue;
    public DateTime? Updated { get ; set; }
}