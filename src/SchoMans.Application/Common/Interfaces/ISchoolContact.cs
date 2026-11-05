namespace SchoMans.Application.Common.Interfaces;

/// <summary>How the school can be reached, for answers that send a person to the staff (the chatbot).</summary>
public interface ISchoolContact
{
    string Contact { get; }
}
