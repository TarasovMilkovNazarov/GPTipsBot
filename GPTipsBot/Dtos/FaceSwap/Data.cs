namespace GPTipsBot.Dtos.FaceSwap
{
    public class Data
    {
        public string version { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public int type { get; set; }
        public List<Option> options { get; set; }
        public ApplicationCommand application_command { get; set; }
        public Attachment[] attachments { get; set; }
    }
}