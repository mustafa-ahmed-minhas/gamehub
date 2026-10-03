using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GameHub.Helpers
{
    public static class EnumDisplayHelper
    {
        public static string GetDisplayName(this Enum value)
        {
            var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
            return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
        }
    }
}
