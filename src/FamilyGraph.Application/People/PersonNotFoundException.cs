namespace FamilyGraph.Application.People;

public sealed class PersonNotFoundException()
    : NotFoundException("person.not_found", "This profile does not exist.");
