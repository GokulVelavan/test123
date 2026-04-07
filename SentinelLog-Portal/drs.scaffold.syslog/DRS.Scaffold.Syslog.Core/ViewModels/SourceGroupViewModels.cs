namespace DRS.Scaffold.Syslog.Core.ViewModels;

public record SourceGroupDto(
    long        Id,
    string      Name,
    string?     Description,
    string?     Color,
    List<long>  MemberIds,
    DateTimeOffset CreatedAt);

public record CreateSourceGroupRequest(
    string  Name,
    string? Description,
    string? Color);

public record UpdateSourceGroupRequest(
    string  Name,
    string? Description,
    string? Color);

public record UpdateGroupMembersRequest(List<long> SourceIds);
