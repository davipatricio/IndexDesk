namespace IndexDesk.BuildingBlocks.Persistence.Entities;

public class PermissionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public ICollection<RolePermissionEntity> RolePermissions { get; set; } =
        new List<RolePermissionEntity>();
}
