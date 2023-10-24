namespace GPTipsBot.Extensions
{
    public static class StringExtensions
    {
        public static string Truncate(this string s, int length)
        {
            return s.Length > length ? s[..length] : s;
        }
    }
}
