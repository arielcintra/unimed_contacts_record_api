using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Domain.Contacts;
using NSubstitute;

namespace ContactsRecord.Application.Tests.Contacts;

/// <summary>Activate/deactivate and favorite/unfavorite share the same load → change → save flow.</summary>
public class ChangeContactStateTests : ContactServiceTestsBase
{
    [Fact]
    public async Task Deactivate_persists_inactive_contact()
    {
        var contact = GivenStoredContact();

        var output = await Service.DeactivateAsync(contact.Id, CancellationToken.None);

        Assert.False(output.IsActive);
        Assert.Equal(Now, output.UpdatedAt);
        await Repository.Received(1).UpdateAsync(contact, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activate_persists_active_contact()
    {
        var contact = GivenStoredContact(isActive: false);

        var output = await Service.ActivateAsync(contact.Id, CancellationToken.None);

        Assert.True(output.IsActive);
    }

    [Fact]
    public async Task Mark_and_unmark_favorite_persist_the_flag()
    {
        var contact = GivenStoredContact();

        Assert.True((await Service.MarkAsFavoriteAsync(contact.Id, CancellationToken.None)).IsFavorite);
        Assert.False((await Service.UnmarkAsFavoriteAsync(contact.Id, CancellationToken.None)).IsFavorite);
        await Repository.Received(2).UpdateAsync(contact, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fails_for_unknown_contact()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => Service.DeactivateAsync(Guid.NewGuid(), CancellationToken.None));

        await Repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }
}
