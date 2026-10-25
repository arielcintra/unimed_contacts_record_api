using ContactsRecord.Application.Common.Exceptions;
using ContactsRecord.Application.Contacts;
using ContactsRecord.Domain.Common;
using ContactsRecord.Domain.Contacts;
using NSubstitute;

namespace ContactsRecord.Application.Tests.Contacts;

public class CreateContactTests : ContactServiceTestsBase
{
    [Fact]
    public async Task Stores_new_contact_with_normalized_data()
    {
        var output = await Service.CreateAsync(Input("(11) 98765-4321"), CancellationToken.None);

        Assert.Equal("11987654321", output.Phone);
        Assert.True(output.IsActive);
        Assert.Equal(Now, output.CreatedAt);
        await Repository.Received(1).AddAsync(
            Arg.Is<Contact>(c => c.Id == output.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Treats_blank_email_as_empty()
    {
        var output = await Service.CreateAsync(Input(email: "  "), CancellationToken.None);

        Assert.Null(output.Email);
    }

    [Fact]
    public async Task Rejects_phone_of_an_active_contact()
    {
        GivenStoredContact("11987654321");

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => Service.CreateAsync(Input("+55 11 98765-4321"), CancellationToken.None));

        Assert.Equal(ContactMessages.PhoneInUse, error.Message);
        await Repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Rejects_phone_of_an_inactive_contact_suggesting_reactivation()
    {
        GivenStoredContact("11987654321", isActive: false);

        var error = await Assert.ThrowsAsync<ConflictException>(
            () => Service.CreateAsync(Input("11987654321"), CancellationToken.None));

        Assert.Equal(ContactMessages.PhoneInUseByInactive, error.Message);
    }

    [Fact]
    public async Task Rejects_invalid_data_without_touching_the_repository()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => Service.CreateAsync(Input("123"), CancellationToken.None));

        Assert.Empty(Repository.ReceivedCalls());
    }
}
