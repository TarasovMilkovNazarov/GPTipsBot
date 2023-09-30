namespace GPTipsBot.Extensions
{
    public static class StringExtensions
    {
        public static string Truncate(this string s, int length)
        {
            if(s.Length > length) 
                return s.Substring(0, length);
            return s;
        }
    }
}
