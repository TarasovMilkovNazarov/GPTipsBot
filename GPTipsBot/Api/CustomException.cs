namespace GPTipsBot.Api
{
    public class CustomException : Exception
    {
        public CustomException(string error) : base(error)
        {
            
        }
    }
}
