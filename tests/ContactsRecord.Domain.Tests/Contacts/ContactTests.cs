using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Contacts;

namespace ContactsRecord.Domain.Tests.Contacts;

public class ContactTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = CreatedAt.AddHours(1);

    private static Contact NewContact(string name = "Ana Silva") =>
        Contact.Create(name, Email.Create("ana@example.com"), PhoneNumber.Create("11987654321"), CreatedAt);

    [Fact]
    public void Create_starts_active_and_not_favorite()
    {
        var contact = NewContact();

        Assert.NotEqual(Guid.Empty, contact.Id);
        Assert.True(contact.IsActive);
        Assert.False(contact.IsFavorite);
        Assert.Equal(CreatedAt, contact.CreatedAt);
        Assert.Equal(CreatedAt, contact.UpdatedAt);
    }

    [Fact]
    public void Create_trims_name()
    {
        Assert.Equal("Ana Silva", NewContact("  Ana Silva  ").Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_name(string name)
    {
        Assert.Throws<DomainException>(() => NewContact(name));
    }

    [Fact]
    public void Create_rejects_name_longer_than_max_length()
    {
        Assert.Throws<DomainException>(() => NewContact(new string('a', Contact.NameMaxLength + 1)));
    }

    [Fact]
    public void Create_requires_phone()
    {
        Assert.Throws<DomainException>(() => Contact.Create("Ana", null, null!, CreatedAt));
    }

    [Fact]
    public void Update_replaces_data_and_keeps_creation_date()
    {
        var contact = NewContact();
        var phone = PhoneNumber.Create("2134567890");

        contact.Update("Ana Souza", null, phone, Later);

        Assert.Equal("Ana Souza", contact.Name);
        Assert.Null(contact.Email);
        Assert.Equal(phone, contact.Phone);
        Assert.Equal(CreatedAt, contact.CreatedAt);
        Assert.Equal(Later, contact.UpdatedAt);
    }

    [Fact]
    public void Deactivate_and_activate_toggle_status()
    {
        var contact = NewContact();

        contact.Deactivate(Later);
        Assert.False(contact.IsActive);
        Assert.Equal(Later, contact.UpdatedAt);

        contact.Activate(Later.AddHours(1));
        Assert.True(contact.IsActive);
    }

    [Fact]
    public void Mark_and_unmark_favorite_toggle_flag()
    {
        var contact = NewContact();

        contact.MarkAsFavorite(Later);
        Assert.True(contact.IsFavorite);
        Assert.Equal(Later, contact.UpdatedAt);

        contact.UnmarkAsFavorite(Later.AddHours(1));
        Assert.False(contact.IsFavorite);
    }

    [Fact]
    public void Repeating_a_state_transition_changes_nothing()
    {
        var contact = NewContact();

        contact.Activate(Later);
        contact.UnmarkAsFavorite(Later);

        Assert.True(contact.IsActive);
        Assert.False(contact.IsFavorite);
        Assert.Equal(CreatedAt, contact.UpdatedAt);
    }
}
