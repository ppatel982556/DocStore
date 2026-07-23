namespace Repositories.Helpers
{
    public static class FileSizeHelper
    {
        public static string Format(long? bytes)
        {
            if (!bytes.HasValue)
                return "-";

            double size = bytes.Value;

            string[] units = { "B", "KB", "MB", "GB", "TB" };

            int index = 0;

            while (size >= 1024 && index < units.Length - 1)
            {
                size /= 1024;
                index++;
            }

            return $"{size:0.##} {units[index]}";
        }
    }
}