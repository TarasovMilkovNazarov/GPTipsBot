using System.Text.RegularExpressions;

namespace GPTipsBot.Utilities;

public static class StringUtilities
{
    public static string? Base64Encode(string? plainText)
    {
        if (plainText is null)
            return plainText;
        
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }
    
    public static string Base64Decode(string base64EncodedData) 
    {
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }
    
    public static string? EscapeTextForMarkdown2(string? input)
    {
        if (input is null)
            return input;
        
        var slash = new Regex(@"(\\[\w:])");
        foreach (var s in new[] { "*", "[", "]", "(", ")", "~", ">", "#", "+", "-", "=", "|", "{", "}", ".", "!" })
            input = input.Replace(s, @$"\{s}");
        input = slash.Replace(input, @"\$1");
        return input;
    }
}