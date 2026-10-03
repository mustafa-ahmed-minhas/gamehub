using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GameHub.Helpers
{
    public static class TempDataToastExtensions
    {
        public static void SetToast(this ITempDataDictionary tempData, string type, string title, string message)
        {
            tempData["ToastType"] = type;
            tempData["ToastTitle"] = title;
            tempData["ToastMessage"] = message;
        }
    }
}
