namespace ContactsRecord.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; }

    // Identity equality: two instances with the same Id are the same entity.
    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    public override int GetHashCode() => Id.GetHashCode();
}
