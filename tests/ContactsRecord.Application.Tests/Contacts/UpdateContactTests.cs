using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Domain.Contacts;
using NSubstitute;

namespace ContactsRecord.Application.Tests.Contacts;

public class UpdateContactTests : ContactServiceTestsBase
{
    [Fact]
    public async Task Updates_data_and_last_change_date()
    {
        var contact = GivenStoredContact("11987654321");

        var output = await Service.UpdateAsync(contact.Id, Input("11987654321"), CancellationToken.None);

        Assert.Equal("Ana Silva", output.Name);
        Assert.Equal(Yesterday, output.CreatedAt);
        Assert.Equal(Now, output.UpdatedAt);
        await Repository.Received(1).UpdateAsync(contact, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_phone_that_belongs_to_another_contact()
    {
        var contact = GivenStoredContact("11987654321");
        GivenStoredContact("21998765432");

        await Assert.ThrowsAsync<ConflictException>(
            () => Service.UpdateAsync(contact.Id, Input("21998765432"), CancellationToken.None));

        await Repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Fails_for_unknown_contact()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => Service.UpdateAsync(Guid.NewGuid(), Input(), CancellationToken.None));

        await Repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }
}
