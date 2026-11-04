using Microsoft.Extensions.Options;
using SchoMans.Application.Common.Interfaces;

namespace SchoMans.Infrastructure.Time;

public sealed class SchoolContact(IOptions<SchoolOptions> options) : ISchoolContact
{
    public string Contact => options.Value.Contact;
}
