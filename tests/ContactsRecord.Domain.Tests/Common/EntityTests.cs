using ContactsRecord.Domain.Common;

namespace ContactsRecord.Domain.Tests.Common;

public class EntityTests
{
    private sealed class FakeEntity : Entity
    {
        public FakeEntity(Guid id) => Id = id;
    }

    [Fact]
    public void Entities_with_same_id_are_equal()
    {
        var id = Guid.NewGuid();

        Assert.Equal(new FakeEntity(id), new FakeEntity(id));
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        Assert.NotEqual(new FakeEntity(Guid.NewGuid()), new FakeEntity(Guid.NewGuid()));
    }

    [Fact]
    public void Entities_without_id_are_never_equal()
    {
        Assert.NotEqual(new FakeEntity(Guid.Empty), new FakeEntity(Guid.Empty));
    }
}
