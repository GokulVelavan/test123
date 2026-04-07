using System.ComponentModel.DataAnnotations;

namespace DRS.Scaffold.Syslog.Core.ViewModels;

public class SavedSearchDto
{
    public long    Id          { get; set; }
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string  QueryJson   { get; set; } = "{}";
    public long?   UserId      { get; set; }
    public string? UserName    { get; set; }
    public bool    IsShared    { get; set; }
    public DateTimeOffset CreatedAt  { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class CreateSavedSearchRequest
{
    [Required, MaxLength(150)]
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required]
    public string  QueryJson   { get; set; } = "{}";
    public long?   UserId      { get; set; }
    public bool    IsShared    { get; set; } = false;
}

public class UpdateSavedSearchRequest
{
    [MaxLength(150)]
    public string? Name        { get; set; }
    public string? Description { get; set; }
    public string? QueryJson   { get; set; }
    public bool?   IsShared    { get; set; }
}
