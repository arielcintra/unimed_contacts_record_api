using ContactsRecord.Application.Common.Dtos;
using ContactsRecord.Application.Contacts.Dtos;
using ContactsRecord.Domain.Contacts;
using NSubstitute;

namespace ContactsRecord.Application.Tests.Contacts;

public class SearchContactsTests : ContactServiceTestsBase
{
    [Fact]
    public async Task Normalizes_query_before_searching()
    {
        var query = new ContactQueryDto { Page = 0, PageSize = 1000, Search = "   " };
        Repository.SearchAsync(default!, default)
            .ReturnsForAnyArgs(new PagedResultDto<Contact>([], 1, ContactQueryDto.MaxPageSize, 0));

        await Service.SearchAsync(query, CancellationToken.None);

        await Repository.Received(1).SearchAsync(
            new ContactQueryDto { Page = 1, PageSize = ContactQueryDto.MaxPageSize, Search = null },
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_page_with_contacts_mapped_to_output()
    {
        var contact = Contact.Create("Ana", null, PhoneNumber.Create("11987654321"), Now);
        Repository.SearchAsync(default!, default)
            .ReturnsForAnyArgs(new PagedResultDto<Contact>([contact], 2, 10, 11));

        var result = await Service.SearchAsync(new ContactQueryDto(), CancellationToken.None);

        Assert.Equal(contact.Id, Assert.Single(result.Items).Id);
        Assert.Equal((2, 10, 11), (result.Page, result.PageSize, result.TotalCount));
    }
}
