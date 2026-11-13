using GameHub.Models.Enums;

namespace GameHub.Models.ViewModels
{
    public class RoleMatrixViewModel
    {
        public List<RoleMatrixRow> Roles { get; set; } = new();
        public List<(string Key, string Name, string Description)> Catalog { get; set; } = new();
    }

    public class RoleMatrixRow
    {
        public UserRole Role { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public bool IsSystemAdmin { get; set; }
        public HashSet<string> AllowedModules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
